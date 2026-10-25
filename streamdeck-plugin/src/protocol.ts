/**
 * Messages du protocole StreamPlug v1 (voir ../../protocol/README.md).
 * Unités SI : mètres, mètres par seconde, poussée de 0 à 1.
 */

export const PROTOCOL_VERSION = 1;

export type VesselStatus = "connected" | "noVessel" | "notControllable";
export type SpeedMode = "surface" | "orbit" | "target";

export type ErrorCode =
	| "BAD_REQUEST"
	| "UNKNOWN_COMMAND"
	| "NO_VESSEL"
	| "NOT_CONTROLLABLE"
	| "REJECTED_BY_GAME"
	| "INTERNAL";

export interface HelloMessage {
	v: 1;
	type: "hello";
	mod: string;
	game: string;
}

export interface StateMessage {
	v: 1;
	type: "state";
	seq: number;
	status: VesselStatus;
	gameState?: string | null;
	vessel?: { name: string | null };
	altitude?: { seaLevel: number | null; surface: number | null };
	speed?: { mode: SpeedMode; surface: number | null; orbital: number | null; target: number | null };
	throttle?: number;
	sas?: { enabled: boolean; mode: string | null };
	rcs?: { enabled: boolean };
}

export interface AckMessage {
	v: 1;
	type: "ack";
	id: string;
}

export interface ErrorMessage {
	v: 1;
	type: "error";
	id?: string;
	code: ErrorCode;
	message: string;
}

export interface PongMessage {
	v: 1;
	type: "pong";
}

export type ServerMessage = HelloMessage | StateMessage | AckMessage | ErrorMessage | PongMessage;

const SERVER_TYPES = new Set(["hello", "state", "ack", "error", "pong"]);
const STATUSES = new Set<string>(["connected", "noVessel", "notControllable"]);

/**
 * Analyse un message reçu du mod. Retourne `undefined` pour tout message invalide ou de type inconnu :
 * un champ ou un type inconnu doit être ignoré (compatibilité ascendante), jamais faire planter le plugin.
 */
export function parseServerMessage(text: string): ServerMessage | undefined {
	let value: unknown;
	try {
		value = JSON.parse(text);
	} catch {
		return undefined;
	}

	if (typeof value !== "object" || value === null) {
		return undefined;
	}

	const message = value as Record<string, unknown>;
	if (message["v"] !== PROTOCOL_VERSION || typeof message["type"] !== "string" || !SERVER_TYPES.has(message["type"])) {
		return undefined;
	}

	switch (message["type"]) {
		case "state":
			return typeof message["seq"] === "number" && typeof message["status"] === "string" && STATUSES.has(message["status"])
				? (message as unknown as StateMessage)
				: undefined;
		case "ack":
			return typeof message["id"] === "string" ? (message as unknown as AckMessage) : undefined;
		case "error":
			return typeof message["code"] === "string" ? (message as unknown as ErrorMessage) : undefined;
		default:
			return message as unknown as ServerMessage;
	}
}

export interface CommandRequest {
	v: 1;
	type: "command";
	id: string;
	name: string;
	args?: Record<string, unknown>;
}
