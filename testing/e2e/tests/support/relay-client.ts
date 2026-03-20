import { readFileSync } from "node:fs";
import http from "node:http";

type RelaySource = {
  sourceId: string;
  displayName: string;
};

const RELAY_HOST = "127.0.0.1";
const RELAY_PORT = 17455;
const RETRY_CODES = new Set(["ECONNRESET", "ECONNREFUSED", "EPIPE", "ETIMEDOUT"]);

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isRetryableRelayError(error: unknown): boolean {
  if (!(error instanceof Error)) {
    return false;
  }

  const code = (error as NodeJS.ErrnoException).code;
  return typeof code === "string" && RETRY_CODES.has(code);
}

function requestJson(method: string, path: string, body?: unknown): Promise<unknown> {
  return new Promise((resolve, reject) => {
    const payload = body == null ? undefined : JSON.stringify(body);
    const req = http.request(
      {
        host: RELAY_HOST,
        port: RELAY_PORT,
        path,
        method,
        headers: payload
          ? {
              "Content-Type": "application/json",
              "Content-Length": Buffer.byteLength(payload),
            }
          : undefined,
      },
      (res) => {
        let chunks = "";
        res.setEncoding("utf-8");
        res.on("data", (chunk) => {
          chunks += chunk;
        });
        res.on("end", () => {
          if ((res.statusCode ?? 500) < 200 || (res.statusCode ?? 500) > 299) {
            reject(new Error(`Relay ${method} ${path} failed with ${res.statusCode}: ${chunks}`));
            return;
          }

          if (!chunks) {
            resolve({});
            return;
          }

          try {
            resolve(JSON.parse(chunks));
          } catch (error) {
            reject(error);
          }
        });
      },
    );

    req.on("error", reject);
    if (payload) {
      req.write(payload);
    }
    req.end();
  });
}

async function requestJsonWithRetry(method: string, path: string, body?: unknown): Promise<unknown> {
  let lastError: unknown;
  for (let attempt = 0; attempt < 4; attempt++) {
    try {
      return await requestJson(method, path, body);
    } catch (error) {
      lastError = error;
      if (!isRetryableRelayError(error) || attempt >= 3) {
        throw error;
      }
      await delay(250 * (attempt + 1));
    }
  }

  throw lastError instanceof Error ? lastError : new Error("Relay request failed.");
}

export async function fetchRelaySources(): Promise<RelaySource[]> {
  const json = (await requestJsonWithRetry("GET", "/sources")) as unknown;
  if (!Array.isArray(json)) {
    return [];
  }

  return json
    .map((item) => ({
      sourceId: String((item as { sourceId?: string }).sourceId ?? "").trim(),
      displayName: String((item as { displayName?: string }).displayName ?? "").trim(),
    }))
    .filter((item) => item.sourceId.length > 0);
}

export async function uploadRelayFrame(sourceId: string, screenshotPath: string): Promise<void> {
  const bytes = readFileSync(screenshotPath);
  await requestJsonWithRetry("POST", "/frame", {
    sourceId,
    pngBase64: bytes.toString("base64"),
  });
}

