import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { ModClient, type ConnectionState } from "../src/connection/ModClient.js";
import type { ServerMessage } from "../src/protocol.js";
import { FakeSocket, TestMod, hello, state, waitFor } from "./helpers.js";

describe("ModClient avec de faux sockets (temps simulé)", () => {
	let sockets: FakeSocket[];
	let urls: string[];

	function newClient(options: Partial<ConstructorParameters<typeof ModClient>[0]> = {}): ModClient {
		return new ModClient({
			url: "ws://127.0.0.1:8765/",
			createSocket: (url) => {
				urls.push(url);
				const socket = new FakeSocket();
				sockets.push(socket);
				return socket;
			},
			...options,
		});
	}

	const last = (): FakeSocket => sockets.at(-1) as FakeSocket;

	beforeEach(() => {
		vi.useFakeTimers();
		sockets = [];
		urls = [];
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	it("est déconnecté tant qu'il n'est pas démarré", () => {
		const client = newClient();
		expect(client.state).toBe("disconnected");
		expect(sockets).toHaveLength(0);
	});

	it("passe par connecting puis connected", () => {
		const client = newClient();
		const states: ConnectionState[] = [];
		client.onConnectionChange((s) => states.push(s));

		client.start();
		expect(client.state).toBe("connecting");
		last().open();
		expect(client.state).toBe("connected");
		expect(states).toEqual(["connecting", "connected"]);
	});

	it("start est idempotent", () => {
		const client = newClient();
		client.start();
		client.start();
		expect(sockets).toHaveLength(1);
	});

	it("remet les messages valides aux abonnés et ignore les autres", () => {
		const client = newClient();
		const received: ServerMessage[] = [];
		client.onMessage((m) => received.push(m));
		client.start();
		last().open();

		last().receive(hello);
		last().receive("pas du json");
		last().receive({ v: 1, type: "inconnu" });
		last().receive(state(1));

		expect(received.map((m) => m.type)).toEqual(["hello", "state"]);
	});

	it("un abonné qui plante n'empêche pas les autres de recevoir", () => {
		const client = newClient();
		const good = vi.fn();
		client.onMessage(() => {
			throw new Error("boum");
		});
		client.onMessage(good);
		client.start();
		last().open();
		last().receive(state(1));
		expect(good).toHaveBeenCalledTimes(1);
	});

	it("se reconnecte avec un délai croissant 1 s, 2 s, 4 s puis plafonné à 5 s", () => {
		const client = newClient();
		client.start();

		const times: number[] = [];
		const startedAt = Date.now();
		for (let i = 0; i < 5; i++) {
			last().fail();
			const before = sockets.length;
			let waited = 0;
			while (sockets.length === before) {
				vi.advanceTimersByTime(100);
				waited += 100;
				expect(waited).toBeLessThanOrEqual(6000);
			}
			times.push(Date.now() - startedAt);
		}

		const gaps = times.map((time, index) => time - (times[index - 1] ?? 0));
		expect(gaps).toEqual([1000, 2000, 4000, 5000, 5000]);
	});

	it("repart du délai minimal après une connexion réussie", () => {
		const client = newClient();
		client.start();
		last().fail();
		vi.advanceTimersByTime(1000);
		last().fail();
		vi.advanceTimersByTime(2000);
		last().open();
		last().dropFromRemote();

		const before = sockets.length;
		vi.advanceTimersByTime(999);
		expect(sockets).toHaveLength(before);
		vi.advanceTimersByTime(1);
		expect(sockets).toHaveLength(before + 1);
	});

	it("est déconnecté pendant l'attente avant la reconnexion", () => {
		const client = newClient();
		client.start();
		last().open();
		last().dropFromRemote();
		expect(client.state).toBe("disconnected");
	});

	it("considère la connexion perdue sans message pendant le délai de péremption", () => {
		const client = newClient({ staleAfterMs: 3000 });
		const states: ConnectionState[] = [];
		client.onConnectionChange((s) => states.push(s));
		client.start();
		last().open();

		vi.advanceTimersByTime(2999);
		expect(client.state).toBe("connected");
		vi.advanceTimersByTime(1);
		expect(client.state).toBe("disconnected");
		expect(sockets[0]?.terminated).toBe(true);

		vi.advanceTimersByTime(1000);
		expect(sockets).toHaveLength(2);
	});

	it("chaque message repousse la péremption", () => {
		const client = newClient({ staleAfterMs: 3000 });
		client.start();
		last().open();

		for (let i = 0; i < 5; i++) {
			vi.advanceTimersByTime(2500);
			last().receive(state(i));
		}

		expect(client.state).toBe("connected");
	});

	it("reconnectNow ne fait pas attendre le délai après un échec", () => {
		const client = newClient();
		client.start();
		last().fail();
		expect(sockets).toHaveLength(1);

		client.reconnectNow();
		expect(sockets).toHaveLength(2);

		// L'ancienne minuterie de reconnexion est annulée : aucune troisième tentative.
		vi.advanceTimersByTime(10_000);
		expect(sockets).toHaveLength(2);
	});

	it("reconnectNow remplace une connexion ouverte", () => {
		const client = newClient();
		client.start();
		last().open();

		client.reconnectNow();

		expect(sockets).toHaveLength(2);
		expect(sockets[0]?.terminated).toBe(true);
	});

	it("reconnectNow n'a aucun effet si le client est arrêté", () => {
		const client = newClient();
		client.reconnectNow();
		expect(sockets).toHaveLength(0);
	});

	it("stop ferme tout et empêche toute reconnexion", () => {
		const client = newClient();
		client.start();
		last().open();
		client.stop();
		expect(client.state).toBe("disconnected");
		vi.advanceTimersByTime(60_000);
		expect(sockets).toHaveLength(1);
	});

	it("peut être redémarré après un arrêt", () => {
		const client = newClient();
		client.start();
		client.stop();
		client.start();
		expect(sockets).toHaveLength(2);
	});

	it("reconfigure change l'adresse et se reconnecte aussitôt", () => {
		const client = newClient();
		client.start();
		last().open();
		client.reconfigure("ws://127.0.0.1:9000/");
		expect(urls).toEqual(["ws://127.0.0.1:8765/", "ws://127.0.0.1:9000/"]);
		expect(client.url).toBe("ws://127.0.0.1:9000/");
	});

	it("reconfigure avec la même adresse ne fait rien", () => {
		const client = newClient();
		client.start();
		client.reconfigure("ws://127.0.0.1:8765/");
		expect(sockets).toHaveLength(1);
	});

	it("reconfigure avant le démarrage change seulement l'adresse", () => {
		const client = newClient();
		client.reconfigure("ws://127.0.0.1:9000/");
		expect(sockets).toHaveLength(0);
		client.start();
		expect(urls).toEqual(["ws://127.0.0.1:9000/"]);
	});

	it("ignore les événements d'un ancien socket", () => {
		const client = newClient();
		client.start();
		const old = last();
		client.reconnectNow();
		const states: ConnectionState[] = [];
		client.onConnectionChange((s) => states.push(s));

		old.open();
		old.receive(state(1));
		expect(states).toEqual([]);
	});

	it("survit à une création de socket qui échoue", () => {
		let attempts = 0;
		const client = new ModClient({
			url: "ws://x/",
			createSocket: () => {
				attempts++;
				if (attempts === 1) {
					throw new Error("URL invalide");
				}
				return new FakeSocket();
			},
		});
		client.start();
		vi.advanceTimersByTime(1000);
		expect(attempts).toBe(2);
	});

	describe("commandes", () => {
		it("envoie la commande au format du protocole et résout sur ack", async () => {
			const client = newClient();
			client.start();
			last().open();

			const outcome = client.sendCommand("setThrottle", { value: 0.5 });
			const sent = last().lastCommand;
			expect(sent).toMatchObject({ v: 1, type: "command", name: "setThrottle", args: { value: 0.5 } });
			expect(sent?.id).toMatch(/^sd-\d+$/);

			last().receive({ v: 1, type: "ack", id: sent?.id });
			await expect(outcome).resolves.toEqual({ ok: true });
		});

		it("omet args quand il n'y en a pas", () => {
			const client = newClient();
			client.start();
			last().open();
			void client.sendCommand("toggleSas");
			expect(last().lastCommand).not.toHaveProperty("args");
		});

		it("résout sur error avec le code et le message du jeu", async () => {
			const client = newClient();
			client.start();
			last().open();

			const outcome = client.sendCommand("toggleRcs");
			last().receive({ v: 1, type: "error", id: last().lastCommand?.id, code: "REJECTED_BY_GAME", message: "RCS indisponible" });
			await expect(outcome).resolves.toEqual({ ok: false, code: "REJECTED_BY_GAME", message: "RCS indisponible" });
		});

		it("associe chaque réponse à sa commande même dans le désordre", async () => {
			const client = newClient();
			client.start();
			last().open();

			const first = client.sendCommand("toggleSas");
			const firstId = last().lastCommand?.id;
			const second = client.sendCommand("toggleRcs");
			const secondId = last().lastCommand?.id;
			expect(firstId).not.toBe(secondId);

			last().receive({ v: 1, type: "error", id: secondId, code: "NO_VESSEL", message: "x" });
			last().receive({ v: 1, type: "ack", id: firstId });

			await expect(first).resolves.toEqual({ ok: true });
			await expect(second).resolves.toMatchObject({ ok: false, code: "NO_VESSEL" });
		});

		it("ignore un ack ou une erreur pour une commande inconnue", async () => {
			const client = newClient();
			client.start();
			last().open();
			const outcome = client.sendCommand("toggleSas");

			last().receive({ v: 1, type: "ack", id: "inconnu" });
			last().receive({ v: 1, type: "error", id: "inconnu", code: "INTERNAL", message: "x" });
			last().receive({ v: 1, type: "error", code: "BAD_REQUEST", message: "sans id" });

			last().receive({ v: 1, type: "ack", id: last().lastCommand?.id });
			await expect(outcome).resolves.toEqual({ ok: true });
		});

		it("échoue tout de suite sans connexion", async () => {
			const client = newClient();
			await expect(client.sendCommand("toggleSas")).resolves.toMatchObject({ ok: false, code: "NOT_CONNECTED" });
			client.start();
			await expect(client.sendCommand("toggleSas")).resolves.toMatchObject({ ok: false, code: "NOT_CONNECTED" });
			expect(sockets[0]?.sent).toEqual([]);
		});

		it("expire sans réponse", async () => {
			const client = newClient({ commandTimeoutMs: 3000 });
			client.start();
			last().open();
			const outcome = client.sendCommand("toggleSas");
			vi.advanceTimersByTime(2999);
			last().receive(state(1)); // le jeu est vivant mais ne répond pas à la commande
			vi.advanceTimersByTime(1);
			await expect(outcome).resolves.toMatchObject({ ok: false, code: "TIMEOUT" });
		});

		it("une réponse tardive après expiration est ignorée sans erreur", async () => {
			const client = newClient({ commandTimeoutMs: 100 });
			client.start();
			last().open();
			const outcome = client.sendCommand("toggleSas");
			const id = last().lastCommand?.id;
			vi.advanceTimersByTime(100);
			await outcome;
			expect(() => last().receive({ v: 1, type: "ack", id })).not.toThrow();
		});

		it("fait échouer les commandes en attente quand la connexion tombe", async () => {
			const client = newClient();
			client.start();
			last().open();
			const outcome = client.sendCommand("toggleSas");
			last().dropFromRemote();
			await expect(outcome).resolves.toMatchObject({ ok: false, code: "NOT_CONNECTED" });
		});

		it("fait échouer les commandes en attente à l'arrêt", async () => {
			const client = newClient();
			client.start();
			last().open();
			const outcome = client.sendCommand("toggleSas");
			client.stop();
			await expect(outcome).resolves.toMatchObject({ ok: false, code: "NOT_CONNECTED" });
		});

		it("résout NOT_CONNECTED si l'envoi lève une exception", async () => {
			const client = newClient();
			client.start();
			last().open();
			last().send = () => {
				throw new Error("socket fermé");
			};
			await expect(client.sendCommand("toggleSas")).resolves.toMatchObject({ ok: false, code: "NOT_CONNECTED", message: "socket fermé" });
		});
	});
});

describe("ModClient avec un vrai serveur WebSocket local", () => {
	const fast = { minReconnectDelayMs: 20, maxReconnectDelayMs: 80, staleAfterMs: 400, commandTimeoutMs: 400 };
	let mod: TestMod;
	let client: ModClient;

	beforeEach(async () => {
		mod = await TestMod.start();
	});

	afterEach(async () => {
		client?.stop();
		await mod.stop();
	});

	it("se connecte, reçoit hello puis les états", async () => {
		client = new ModClient({ url: mod.url, ...fast });
		const received: ServerMessage[] = [];
		client.onMessage((m) => received.push(m));
		client.start();

		await waitFor(() => client.state === "connected", "connexion");
		await waitFor(() => received.some((m) => m.type === "hello"), "hello");

		mod.broadcast(state(1));
		await waitFor(() => received.some((m) => m.type === "state"), "état");
	});

	it("envoie une commande et reçoit l'ack", async () => {
		client = new ModClient({ url: mod.url, ...fast });
		client.start();
		await waitFor(() => client.state === "connected", "connexion");

		await expect(client.sendCommand("setThrottle", { value: 0.7 })).resolves.toEqual({ ok: true });
		expect(mod.received[0]).toMatchObject({ v: 1, type: "command", name: "setThrottle", args: { value: 0.7 } });
	});

	it("remonte le refus du jeu", async () => {
		mod.respond = (command) => ({ v: 1, type: "error", id: String(command["id"]), code: "NOT_CONTROLLABLE", message: "Pas de pilote" });
		client = new ModClient({ url: mod.url, ...fast });
		client.start();
		await waitFor(() => client.state === "connected", "connexion");

		await expect(client.sendCommand("toggleSas")).resolves.toEqual({ ok: false, code: "NOT_CONTROLLABLE", message: "Pas de pilote" });
	});

	it("expire quand le jeu ne répond pas", async () => {
		mod.respond = undefined;
		client = new ModClient({ url: mod.url, ...fast, staleAfterMs: 5000 });
		client.start();
		await waitFor(() => client.state === "connected", "connexion");

		await expect(client.sendCommand("toggleSas")).resolves.toMatchObject({ ok: false, code: "TIMEOUT" });
	});

	it("se reconnecte tout seul quand le serveur coupe la connexion", async () => {
		client = new ModClient({ url: mod.url, ...fast });
		client.start();
		await waitFor(() => client.state === "connected", "première connexion");

		mod.dropClients();

		await waitFor(() => mod.connections === 2 && client.state === "connected", "reconnexion");
	});

	it("se connecte dès que le jeu démarre, même s'il est lancé après le plugin", async () => {
		const port = mod.port;
		await mod.stop();

		client = new ModClient({ url: `ws://127.0.0.1:${port}/`, ...fast });
		client.start();
		await new Promise((resolve) => setTimeout(resolve, 150));
		expect(client.state).not.toBe("connected");

		mod = await TestMod.start(port);
		await waitFor(() => client.state === "connected", "connexion après démarrage du jeu");
	});

	it("détecte un jeu qui ne répond plus (connexion ouverte mais silencieuse) et se reconnecte", async () => {
		mod.sendHello = false;
		client = new ModClient({ url: mod.url, ...fast, staleAfterMs: 150 });
		client.start();

		await waitFor(() => mod.connections >= 2, "reconnexion après silence", 2000);
	});

	it("reste connecté tant que le jeu envoie des états", async () => {
		client = new ModClient({ url: mod.url, ...fast, staleAfterMs: 150 });
		client.start();
		await waitFor(() => client.state === "connected", "connexion");

		const timer = setInterval(() => mod.broadcast(state(1)), 50);
		await new Promise((resolve) => setTimeout(resolve, 500));
		clearInterval(timer);

		expect(mod.connections).toBe(1);
		expect(client.state).toBe("connected");
	});

	it("passe à la nouvelle adresse après reconfigure", async () => {
		const second = await TestMod.start();
		try {
			client = new ModClient({ url: mod.url, ...fast });
			client.start();
			await waitFor(() => mod.connections === 1, "connexion au premier mod");

			client.reconfigure(second.url);
			await waitFor(() => second.connections === 1 && client.state === "connected", "connexion au second mod");
		} finally {
			client.stop();
			await second.stop();
		}
	});
});
