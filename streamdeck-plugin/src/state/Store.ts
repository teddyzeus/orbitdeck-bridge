import type { ConnectionState } from "../connection/ModClient.js";
import type { ServerMessage, StateMessage } from "../protocol.js";

/** Ce que le joueur doit voir : le jeu est-il joignable, et peut-on piloter un véhicule ? */
export type Availability = "offline" | "noVessel" | "notControllable" | "ready";

/** Source des événements de connexion et des messages (implémentée par `ModClient`). */
export interface StoreSource {
	onConnectionChange(listener: (state: ConnectionState) => void): () => void;
	onMessage(listener: (message: ServerMessage) => void): () => void;
}

/**
 * Dernier état connu du jeu. Ignore les états périmés (`seq` décroissant) et oublie tout à la déconnexion :
 * le mod repart de zéro à chaque lancement du jeu, ses numéros de séquence ne sont comparables qu'au sein d'une connexion.
 */
export class Store {
	readonly #listeners = new Set<() => void>();
	readonly #disposers: Array<() => void> = [];

	#connection: ConnectionState = "disconnected";
	#state: StateMessage | undefined;
	#gameVersion: string | undefined;

	constructor(source: StoreSource) {
		this.#disposers.push(
			source.onConnectionChange((state) => this.#setConnection(state)),
			source.onMessage((message) => this.#handleMessage(message)),
		);
	}

	get connection(): ConnectionState {
		return this.#connection;
	}

	/** Dernier état reçu sur la connexion courante ; `undefined` hors connexion. */
	get state(): StateMessage | undefined {
		return this.#state;
	}

	get gameVersion(): string | undefined {
		return this.#gameVersion;
	}

	get availability(): Availability {
		if (this.#connection !== "connected" || this.#state === undefined) {
			return "offline";
		}

		switch (this.#state.status) {
			case "noVessel":
				return "noVessel";
			case "notControllable":
				return "notControllable";
			default:
				return "ready";
		}
	}

	/** Abonne un écouteur appelé à chaque changement visible. Retourne la fonction de désabonnement. */
	subscribe(listener: () => void): () => void {
		this.#listeners.add(listener);
		return () => this.#listeners.delete(listener);
	}

	dispose(): void {
		for (const dispose of this.#disposers.splice(0)) {
			dispose();
		}

		this.#listeners.clear();
	}

	#setConnection(state: ConnectionState): void {
		if (state === this.#connection) {
			return;
		}

		this.#connection = state;
		if (state !== "connected") {
			this.#state = undefined;
			this.#gameVersion = undefined;
		}

		this.#notify();
	}

	#handleMessage(message: ServerMessage): void {
		if (message.type === "hello") {
			this.#gameVersion = message.game;
			this.#notify();
			return;
		}

		if (message.type !== "state") {
			return;
		}

		// Un état de même séquence est un battement de cœur : on l'accepte (il confirme que le jeu est vivant).
		if (this.#state !== undefined && message.seq < this.#state.seq) {
			return;
		}

		this.#state = message;
		this.#notify();
	}

	#notify(): void {
		for (const listener of [...this.#listeners]) {
			try {
				listener();
			} catch {
				// Un abonné défaillant ne doit pas empêcher les autres d'être notifiés.
			}
		}
	}
}
