import type WebSocket from "ws";
import { WebSocketServer } from "ws";

import type { SocketLike } from "../src/connection/ModClient.js";
import type { ServerMessage, StateMessage } from "../src/protocol.js";

type Handlers = {
	open: Array<() => void>;
	message: Array<(data: WebSocket.RawData) => void>;
	close: Array<() => void>;
	error: Array<(error: Error) => void>;
};

/** Faux socket piloté à la main : les tests décident quand il s'ouvre, reçoit un message ou se ferme. */
export class FakeSocket implements SocketLike {
	readonly sent: string[] = [];
	terminated = false;
	readonly #handlers: Handlers = { open: [], message: [], close: [], error: [] };

	on(event: "open", listener: () => void): this;
	on(event: "message", listener: (data: WebSocket.RawData) => void): this;
	on(event: "close", listener: () => void): this;
	on(event: "error", listener: (error: Error) => void): this;
	on(event: keyof Handlers, listener: never): this {
		(this.#handlers[event] as unknown[]).push(listener);
		return this;
	}

	send(data: string): void {
		this.sent.push(data);
	}

	close(): void {
		this.#emitClose();
	}

	terminate(): void {
		this.terminated = true;
		this.#emitClose();
	}

	open(): void {
		this.#handlers.open.forEach((listener) => listener());
	}

	receive(message: unknown): void {
		const text = typeof message === "string" ? message : JSON.stringify(message);
		this.#handlers.message.forEach((listener) => listener(Buffer.from(text)));
	}

	fail(message = "ECONNREFUSED"): void {
		this.#handlers.error.forEach((listener) => listener(new Error(message)));
		this.#emitClose();
	}

	dropFromRemote(): void {
		this.#emitClose();
	}

	/** Dernière commande envoyée par le client, décodée. */
	get lastCommand(): { id: string; name: string; args?: Record<string, unknown> } | undefined {
		const last = this.sent.at(-1);
		return last === undefined ? undefined : JSON.parse(last);
	}

	#emitClose(): void {
		this.#handlers.close.forEach((listener) => listener());
	}
}

export function state(seq: number, overrides: Partial<StateMessage> = {}): StateMessage {
	return {
		v: 1,
		type: "state",
		seq,
		status: "connected",
		gameState: "FlightView",
		vessel: { name: "Kerbal X" },
		altitude: { seaLevel: 1000, surface: 990 },
		speed: { mode: "surface", surface: 100, orbital: 200, target: null },
		throttle: 0.5,
		sas: { enabled: true, mode: "StabilityAssist" },
		rcs: { enabled: false },
		...overrides,
	};
}

export const hello: ServerMessage = { v: 1, type: "hello", mod: "0.1.0", game: "0.2.8.5" };

/** Vrai serveur WebSocket local imitant le mod. */
export class TestMod {
	readonly server: WebSocketServer;
	readonly sockets = new Set<WebSocket>();
	connections = 0;
	readonly received: Array<Record<string, unknown>> = [];
	/** Réponse automatique aux commandes ; `undefined` = ne pas répondre. */
	respond: ((command: Record<string, unknown>) => ServerMessage | undefined) | undefined = (command) => ({
		v: 1,
		type: "ack",
		id: String(command["id"]),
	});
	sendHello = true;

	constructor(port = 0) {
		this.server = new WebSocketServer({ host: "127.0.0.1", port });
		this.server.on("connection", (socket) => {
			this.connections++;
			this.sockets.add(socket);
			socket.on("close", () => this.sockets.delete(socket));
			if (this.sendHello) {
				socket.send(JSON.stringify(hello));
			}

			socket.on("message", (data) => {
				const message = JSON.parse(data.toString()) as Record<string, unknown>;
				this.received.push(message);
				const reply = this.respond?.(message);
				if (reply !== undefined) {
					socket.send(JSON.stringify(reply));
				}
			});
		});
	}

	static async start(port = 0): Promise<TestMod> {
		const mod = new TestMod(port);
		await new Promise<void>((resolve) => mod.server.once("listening", () => resolve()));
		return mod;
	}

	get port(): number {
		return (this.server.address() as { port: number }).port;
	}

	get url(): string {
		return `ws://127.0.0.1:${this.port}/`;
	}

	broadcast(message: ServerMessage): void {
		for (const socket of this.sockets) {
			socket.send(JSON.stringify(message));
		}
	}

	dropClients(): void {
		for (const socket of this.sockets) {
			socket.terminate();
		}
	}

	async stop(): Promise<void> {
		this.dropClients();
		await new Promise<void>((resolve) => this.server.close(() => resolve()));
	}
}

/** Attend qu'une condition devienne vraie (sondage court), ou échoue avec un message clair. */
export async function waitFor(condition: () => boolean, what: string, timeoutMs = 3000): Promise<void> {
	const deadline = Date.now() + timeoutMs;
	while (!condition()) {
		if (Date.now() > deadline) {
			throw new Error(`Délai dépassé en attendant : ${what}`);
		}

		await new Promise((resolve) => setTimeout(resolve, 5));
	}
}
