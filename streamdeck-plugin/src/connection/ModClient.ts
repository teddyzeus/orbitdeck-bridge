import WebSocket from "ws";

import { PROTOCOL_VERSION, parseServerMessage, type CommandRequest, type ErrorCode, type ServerMessage } from "../protocol.js";

export type ConnectionState = "disconnected" | "connecting" | "connected";

/** Résultat d'une commande : acceptée par le jeu, ou refusée (par le jeu, le mod, ou faute de connexion). */
export type CommandOutcome =
	| { ok: true }
	| { ok: false; code: ErrorCode | "NOT_CONNECTED" | "TIMEOUT"; message: string };

/** Sous-ensemble de `WebSocket` utilisé par le client (permet de substituer un faux socket dans les tests). */
export interface SocketLike {
	on(event: "open", listener: () => void): unknown;
	on(event: "message", listener: (data: WebSocket.RawData) => void): unknown;
	on(event: "close", listener: () => void): unknown;
	on(event: "error", listener: (error: Error) => void): unknown;
	send(data: string): void;
	close(): void;
	terminate(): void;
}

export interface ModClientOptions {
	/** Adresse du mod, par exemple `ws://127.0.0.1:8765/`. */
	url: string;
	/** Délai initial avant une reconnexion, doublé à chaque échec jusqu'à `maxReconnectDelayMs`. */
	minReconnectDelayMs?: number;
	maxReconnectDelayMs?: number;
	/** Le mod envoie un état au moins une fois par seconde : sans aucun message pendant ce délai, la connexion est considérée comme perdue. */
	staleAfterMs?: number;
	commandTimeoutMs?: number;
	createSocket?: (url: string) => SocketLike;
	log?: (message: string) => void;
}

type Listener<T> = (value: T) => void;

/**
 * Client du mod StreamPlug : se connecte, se reconnecte tout seul, détecte les connexions mortes et envoie
 * des commandes dont il suit la réponse (`ack` ou `error`).
 */
export class ModClient {
	#url: string;
	readonly #minDelay: number;
	readonly #maxDelay: number;
	readonly #staleAfter: number;
	readonly #commandTimeout: number;
	readonly #createSocket: (url: string) => SocketLike;
	readonly #log: (message: string) => void;

	readonly #connectionListeners = new Set<Listener<ConnectionState>>();
	readonly #messageListeners = new Set<Listener<ServerMessage>>();
	readonly #pending = new Map<string, { resolve: (outcome: CommandOutcome) => void; timer: ReturnType<typeof setTimeout> }>();

	#socket: SocketLike | undefined;
	#state: ConnectionState = "disconnected";
	#running = false;
	#delay: number;
	#reconnectTimer: ReturnType<typeof setTimeout> | undefined;
	#staleTimer: ReturnType<typeof setTimeout> | undefined;
	#nextCommandId = 1;

	constructor(options: ModClientOptions) {
		this.#url = options.url;
		this.#minDelay = options.minReconnectDelayMs ?? 1000;
		this.#maxDelay = options.maxReconnectDelayMs ?? 5000;
		this.#staleAfter = options.staleAfterMs ?? 3000;
		this.#commandTimeout = options.commandTimeoutMs ?? 3000;
		this.#createSocket = options.createSocket ?? ((url) => new WebSocket(url) as unknown as SocketLike);
		this.#log = options.log ?? (() => undefined);
		this.#delay = this.#minDelay;
	}

	get url(): string {
		return this.#url;
	}

	get state(): ConnectionState {
		return this.#state;
	}

	onConnectionChange(listener: Listener<ConnectionState>): () => void {
		this.#connectionListeners.add(listener);
		return () => this.#connectionListeners.delete(listener);
	}

	onMessage(listener: Listener<ServerMessage>): () => void {
		this.#messageListeners.add(listener);
		return () => this.#messageListeners.delete(listener);
	}

	/** Démarre la connexion (et les reconnexions automatiques). Sans effet si déjà démarré. */
	start(): void {
		if (this.#running) {
			return;
		}

		this.#running = true;
		this.#connect();
	}

	/** Change l'adresse du mod. Si le client tourne, il se reconnecte aussitôt à la nouvelle adresse. */
	reconfigure(url: string): void {
		if (url === this.#url) {
			return;
		}

		this.#url = url;
		this.reconnectNow();
	}

	/** Arrête définitivement les connexions et les reconnexions. */
	stop(): void {
		this.#running = false;
		this.#clearTimers();
		this.#dropSocket();
		this.#setState("disconnected");
		this.#failAllPending("NOT_CONNECTED", "Client arrêté.");
	}

	/** Se reconnecte immédiatement, sans attendre le délai (par exemple sur demande de l'utilisateur). */
	reconnectNow(): void {
		if (!this.#running) {
			return;
		}

		this.#clearTimers();
		this.#dropSocket();
		this.#delay = this.#minDelay;
		this.#connect();
	}

	/** Envoie une commande et attend sa réponse. Ne rejette jamais : l'échec est dans le résultat. */
	sendCommand(name: string, args?: Record<string, unknown>): Promise<CommandOutcome> {
		if (this.#state !== "connected" || this.#socket === undefined) {
			return Promise.resolve({ ok: false, code: "NOT_CONNECTED", message: "Le jeu n'est pas connecté." });
		}

		const id = `sd-${this.#nextCommandId++}`;
		const request: CommandRequest = { v: PROTOCOL_VERSION, type: "command", id, name, ...(args ? { args } : {}) };

		return new Promise<CommandOutcome>((resolve) => {
			const timer = setTimeout(() => {
				this.#pending.delete(id);
				resolve({ ok: false, code: "TIMEOUT", message: "Le jeu n'a pas répondu." });
			}, this.#commandTimeout);

			this.#pending.set(id, { resolve, timer });

			try {
				this.#socket?.send(JSON.stringify(request));
			} catch (error) {
				clearTimeout(timer);
				this.#pending.delete(id);
				resolve({ ok: false, code: "NOT_CONNECTED", message: error instanceof Error ? error.message : String(error) });
			}
		});
	}

	#connect(): void {
		this.#setState("connecting");

		let socket: SocketLike;
		try {
			socket = this.#createSocket(this.#url);
		} catch (error) {
			this.#log(`Connexion impossible : ${error instanceof Error ? error.message : String(error)}`);
			this.#scheduleReconnect();
			return;
		}

		this.#socket = socket;

		socket.on("open", () => {
			if (this.#socket !== socket) {
				return;
			}

			this.#delay = this.#minDelay;
			this.#setState("connected");
			this.#armStaleTimer();
		});

		socket.on("message", (data) => {
			if (this.#socket !== socket) {
				return;
			}

			this.#armStaleTimer();
			const message = parseServerMessage(data.toString());
			if (message !== undefined) {
				this.#handleMessage(message);
			}
		});

		socket.on("close", () => this.#handleLost(socket));
		// Une erreur de socket est toujours suivie d'un `close` : la reconnexion est gérée là.
		socket.on("error", (error) => this.#log(`Erreur de connexion : ${error.message}`));
	}

	#handleMessage(message: ServerMessage): void {
		if (message.type === "ack" || (message.type === "error" && message.id !== undefined)) {
			const id = message.id;
			const pending = id === undefined ? undefined : this.#pending.get(id);
			if (id !== undefined && pending !== undefined) {
				clearTimeout(pending.timer);
				this.#pending.delete(id);
				pending.resolve(message.type === "ack" ? { ok: true } : { ok: false, code: message.code, message: message.message });
			}
		}

		for (const listener of [...this.#messageListeners]) {
			try {
				listener(message);
			} catch (error) {
				this.#log(`Erreur dans un abonné : ${error instanceof Error ? error.message : String(error)}`);
			}
		}
	}

	#handleLost(socket: SocketLike): void {
		if (this.#socket !== socket) {
			return;
		}

		this.#socket = undefined;
		this.#clearStaleTimer();
		this.#setState("disconnected");
		this.#failAllPending("NOT_CONNECTED", "Connexion perdue.");

		if (this.#running) {
			this.#scheduleReconnect();
		}
	}

	#scheduleReconnect(): void {
		if (!this.#running || this.#reconnectTimer !== undefined) {
			return;
		}

		const delay = this.#delay;
		this.#delay = Math.min(this.#delay * 2, this.#maxDelay);
		this.#setState("disconnected");
		this.#reconnectTimer = setTimeout(() => {
			this.#reconnectTimer = undefined;
			this.#connect();
		}, delay);
	}

	#armStaleTimer(): void {
		this.#clearStaleTimer();
		this.#staleTimer = setTimeout(() => {
			this.#log("Aucun message du jeu : connexion considérée comme perdue.");
			const socket = this.#socket;
			if (socket !== undefined) {
				this.#handleLost(socket);
				try {
					socket.terminate();
				} catch {
					// Déjà fermé.
				}
			}
		}, this.#staleAfter);
	}

	#clearStaleTimer(): void {
		if (this.#staleTimer !== undefined) {
			clearTimeout(this.#staleTimer);
			this.#staleTimer = undefined;
		}
	}

	#clearTimers(): void {
		this.#clearStaleTimer();
		if (this.#reconnectTimer !== undefined) {
			clearTimeout(this.#reconnectTimer);
			this.#reconnectTimer = undefined;
		}
	}

	#dropSocket(): void {
		const socket = this.#socket;
		this.#socket = undefined;
		if (socket !== undefined) {
			try {
				socket.terminate();
			} catch {
				// Déjà fermé.
			}
		}
	}

	#failAllPending(code: "NOT_CONNECTED", message: string): void {
		for (const [id, pending] of this.#pending) {
			clearTimeout(pending.timer);
			pending.resolve({ ok: false, code, message });
			this.#pending.delete(id);
		}
	}

	#setState(state: ConnectionState): void {
		if (this.#state === state) {
			return;
		}

		this.#state = state;
		for (const listener of [...this.#connectionListeners]) {
			try {
				listener(state);
			} catch (error) {
				this.#log(`Erreur dans un abonné : ${error instanceof Error ? error.message : String(error)}`);
			}
		}
	}
}
