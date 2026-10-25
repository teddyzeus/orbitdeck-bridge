import { describe, expect, it, vi } from "vitest";

import type { ConnectionState } from "../src/connection/ModClient.js";
import type { ServerMessage } from "../src/protocol.js";
import { Store, type StoreSource } from "../src/state/Store.js";
import { hello, state } from "./helpers.js";

/** Source pilotée à la main. */
class FakeSource implements StoreSource {
	#connection: Array<(state: ConnectionState) => void> = [];
	#message: Array<(message: ServerMessage) => void> = [];

	onConnectionChange(listener: (state: ConnectionState) => void): () => void {
		this.#connection.push(listener);
		return () => (this.#connection = this.#connection.filter((l) => l !== listener));
	}

	onMessage(listener: (message: ServerMessage) => void): () => void {
		this.#message.push(listener);
		return () => (this.#message = this.#message.filter((l) => l !== listener));
	}

	connection(value: ConnectionState): void {
		this.#connection.forEach((listener) => listener(value));
	}

	message(value: ServerMessage): void {
		this.#message.forEach((listener) => listener(value));
	}

	get listenerCount(): number {
		return this.#connection.length + this.#message.length;
	}
}

function connectedStore() {
	const source = new FakeSource();
	const store = new Store(source);
	source.connection("connected");
	return { source, store };
}

describe("Store", () => {
	it("démarre hors ligne", () => {
		const store = new Store(new FakeSource());
		expect(store.availability).toBe("offline");
		expect(store.connection).toBe("disconnected");
		expect(store.state).toBeUndefined();
	});

	it("reste hors ligne tant qu'aucun état n'est reçu, même connecté", () => {
		const { store } = connectedStore();
		expect(store.availability).toBe("offline");
	});

	it("est prêt après un état avec véhicule contrôlable", () => {
		const { source, store } = connectedStore();
		source.message(state(1));
		expect(store.availability).toBe("ready");
		expect(store.state?.throttle).toBe(0.5);
	});

	it.each([
		["noVessel", "noVessel"],
		["notControllable", "notControllable"],
	] as const)("statut %s → disponibilité %s", (status, availability) => {
		const { source, store } = connectedStore();
		source.message(state(1, { status }));
		expect(store.availability).toBe(availability);
	});

	it("mémorise la version du jeu annoncée par hello", () => {
		const { source, store } = connectedStore();
		source.message(hello);
		expect(store.gameVersion).toBe("0.2.8.5");
	});

	it("ignore un état plus ancien que le dernier reçu", () => {
		const { source, store } = connectedStore();
		source.message(state(10, { throttle: 0.9 }));
		source.message(state(7, { throttle: 0.1 }));
		expect(store.state?.seq).toBe(10);
		expect(store.state?.throttle).toBe(0.9);
	});

	it("accepte un état de même séquence (battement de cœur)", () => {
		const { source, store } = connectedStore();
		const listener = vi.fn();
		source.message(state(10));
		store.subscribe(listener);
		source.message(state(10));
		expect(listener).toHaveBeenCalledTimes(1);
	});

	it("oublie tout à la déconnexion", () => {
		const { source, store } = connectedStore();
		source.message(hello);
		source.message(state(5));
		source.connection("disconnected");
		expect(store.availability).toBe("offline");
		expect(store.state).toBeUndefined();
		expect(store.gameVersion).toBeUndefined();
	});

	it("repart de zéro après une reconnexion : le mod relancé recommence ses séquences", () => {
		const { source, store } = connectedStore();
		source.message(state(5000));
		source.connection("disconnected");
		source.connection("connected");
		source.message(state(1, { throttle: 0.2 }));
		expect(store.state?.seq).toBe(1);
		expect(store.state?.throttle).toBe(0.2);
	});

	it("notifie les abonnés à chaque changement de connexion et d'état", () => {
		const source = new FakeSource();
		const store = new Store(source);
		const listener = vi.fn();
		store.subscribe(listener);

		source.connection("connecting");
		source.connection("connected");
		source.message(state(1));
		source.message(state(2));
		expect(listener).toHaveBeenCalledTimes(4);
	});

	it("ne notifie pas pour un changement de connexion identique", () => {
		const source = new FakeSource();
		const store = new Store(source);
		const listener = vi.fn();
		store.subscribe(listener);
		source.connection("disconnected");
		expect(listener).not.toHaveBeenCalled();
	});

	it("ignore les messages sans effet sur l'état (ack, pong, erreurs)", () => {
		const { source, store } = connectedStore();
		const listener = vi.fn();
		store.subscribe(listener);
		source.message({ v: 1, type: "pong" });
		source.message({ v: 1, type: "ack", id: "x" });
		source.message({ v: 1, type: "error", code: "NO_VESSEL", message: "x" });
		expect(listener).not.toHaveBeenCalled();
	});

	it("un abonné qui plante n'empêche pas les autres d'être notifiés", () => {
		const { source, store } = connectedStore();
		const good = vi.fn();
		store.subscribe(() => {
			throw new Error("boum");
		});
		store.subscribe(good);
		source.message(state(1));
		expect(good).toHaveBeenCalledTimes(1);
	});

	it("se désabonne", () => {
		const { source, store } = connectedStore();
		const listener = vi.fn();
		const unsubscribe = store.subscribe(listener);
		unsubscribe();
		source.message(state(1));
		expect(listener).not.toHaveBeenCalled();
	});

	it("dispose détache la source", () => {
		const source = new FakeSource();
		const store = new Store(source);
		expect(source.listenerCount).toBe(2);
		store.dispose();
		expect(source.listenerCount).toBe(0);
	});
});
