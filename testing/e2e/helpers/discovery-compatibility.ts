export type CompatibilityStatus =
  | "compatible"
  | "limited"
  | "incompatible"
  | "blocked"
  | "pending";

export interface CompatibilityMatrixRow {
  targetId: string;
  role: "baseline" | "venue" | "additional-older";
  versionString: string;
  discoveryStatus: "pending" | "succeeded" | "failed";
  streamStartStatus: "pending" | "succeeded" | "failed";
  compatibilityStatus: CompatibilityStatus;
  failureCategory: "none" | "compatibility" | "endpoint_unreachable" | "network" | "environment" | "unknown";
  evidenceRef: string;
  notes: string;
}

export function classifyCompatibility(params: {
  discoverySucceeded: boolean;
  streamStartAttempted: boolean;
  streamStartSucceeded: boolean;
  blocked: boolean;
}): CompatibilityStatus {
  if (params.blocked) return "blocked";
  if (params.discoverySucceeded && params.streamStartSucceeded) return "compatible";
  if (params.discoverySucceeded && params.streamStartAttempted && !params.streamStartSucceeded) return "incompatible";
  if (params.discoverySucceeded) return "limited";
  return "pending";
}
