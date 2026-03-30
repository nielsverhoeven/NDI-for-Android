import http from "node:http";

const PORT = 17455;
const TTL_MS = 120000;
const MAX_BODY_BYTES = 16 * 1024 * 1024;

/** @type {Map<string, { sourceId: string, displayName: string, updatedAt: number }>} */
const sources = new Map();
/** @type {Map<string, Buffer>} */
const frames = new Map();

function now() {
  return Date.now();
}

function pruneExpired() {
  const cutoff = now() - TTL_MS;
  for (const [key, value] of sources.entries()) {
    if (value.updatedAt < cutoff) {
      sources.delete(key);
      frames.delete(key);
    }
  }
}

function readJsonBody(req) {
  return new Promise((resolve, reject) => {
    let body = "";
    req.on("data", (chunk) => {
      body += chunk;
      if (body.length > MAX_BODY_BYTES) {
        reject(new Error("Body too large"));
      }
    });
    req.on("end", () => {
      if (!body) {
        resolve({});
        return;
      }
      try {
        resolve(JSON.parse(body));
      } catch (error) {
        reject(error);
      }
    });
    req.on("error", reject);
  });
}

function writeJson(res, statusCode, payload) {
  const content = JSON.stringify(payload);
  res.writeHead(statusCode, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(content),
  });
  res.end(content);
}

const server = http.createServer(async (req, res) => {
  try {
    pruneExpired();

    if (req.method === "GET" && req.url === "/health") {
      writeJson(res, 200, { status: "ok", active: sources.size });
      return;
    }

    if (req.method === "GET" && req.url === "/sources") {
      writeJson(
        res,
        200,
        Array.from(sources.values()).map((item) => ({
          sourceId: item.sourceId,
          displayName: item.displayName,
        })),
      );
      return;
    }

    if (req.method === "POST" && req.url === "/announce") {
      const body = await readJsonBody(req);
      const sourceId = String(body.sourceId ?? "").trim();
      const displayName = String(body.displayName ?? "").trim();
      if (!sourceId) {
        writeJson(res, 400, { error: "sourceId is required" });
        return;
      }

      sources.set(sourceId, {
        sourceId,
        displayName: displayName || sourceId,
        updatedAt: now(),
      });
      writeJson(res, 200, { ok: true });
      return;
    }

    if (req.method === "POST" && req.url === "/revoke") {
      const body = await readJsonBody(req);
      const sourceId = String(body.sourceId ?? "").trim();
      if (sourceId) {
        sources.delete(sourceId);
        frames.delete(sourceId);
      }
      writeJson(res, 200, { ok: true });
      return;
    }

    if (req.method === "POST" && req.url === "/frame") {
      const body = await readJsonBody(req);
      const sourceId = String(body.sourceId ?? "").trim();
      const pngBase64 = String(body.pngBase64 ?? "").trim();
      if (!sourceId || !pngBase64) {
        writeJson(res, 400, { error: "sourceId and pngBase64 are required" });
        return;
      }

      const bytes = Buffer.from(pngBase64, "base64");
      if (bytes.length === 0) {
        writeJson(res, 400, { error: "Invalid pngBase64 payload" });
        return;
      }

      frames.set(sourceId, bytes);
      writeJson(res, 200, { ok: true, size: bytes.length });
      return;
    }

    if (req.method === "GET" && req.url?.startsWith("/frame/")) {
      const sourceId = decodeURIComponent(req.url.slice("/frame/".length));
      const frame = frames.get(sourceId);
      if (!frame) {
        writeJson(res, 404, { error: "Frame not found" });
        return;
      }

      res.writeHead(200, {
        "Content-Type": "image/png",
        "Content-Length": frame.length,
      });
      res.end(frame);
      return;
    }

    writeJson(res, 404, { error: "Not found" });
  } catch (error) {
    writeJson(res, 500, { error: String(error) });
  }
});

server.listen(PORT, "0.0.0.0", () => {
  console.log(`ndi-relay-server listening on ${PORT}`);
});

const shutdown = () => {
  server.close(() => process.exit(0));
};
process.on("SIGTERM", shutdown);
process.on("SIGINT", shutdown);

