import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { DEFAULT_PORT, portFrom, urlFor } from "../src/connection/address.js";
import { parseServerMessage } from "../src/protocol.js";

const examples = path.resolve(import.meta.dirname, "../../protocol/examples");

describe("parseServerMessage", () => {
	it.each(readdirSync(examples).filter((file) => file.startsWith("server-")))("accepte l'exemple officiel %s", (file) => {
		const message = parseServerMessage(readFileSync(path.join(examples, file), "utf8"));
		expect(message).toBeDefined();
		expect(file).toContain(message?.type);
	});

	it("décode un état complet", () => {
		const message = parseServerMessage(readFileSync(path.join(examples, "server-state-connected.json"), "utf8"));
		expect(message).toMatchObject({
			type: "state",
			seq: 1042,
			status: "connected",
			speed: { mode: "surface", target: null },
			throttle: 0.5,
		});
	});

	it.each([
		["pas du json"],
		[""],
		["null"],
		["42"],
		["[1,2]"],
		['{"type":"state","seq":1,"status":"connected"}'],
		['{"v":2,"type":"state","seq":1,"status":"connected"}'],
		['{"v":1,"type":"inconnu"}'],
		['{"v":1,"type":"state","seq":"1","status":"connected"}'],
		['{"v":1,"type":"state","seq":1,"status":"explosé"}'],
		['{"v":1,"type":"ack"}'],
		['{"v":1,"type":"error"}'],
	])("ignore le message invalide %s", (text) => {
		expect(parseServerMessage(text)).toBeUndefined();
	});

	it("ignore les champs inconnus (compatibilité ascendante)", () => {
		const message = parseServerMessage('{"v":1,"type":"pong","nouveau":true}');
		expect(message?.type).toBe("pong");
	});
});

describe("adresse du mod", () => {
	it("utilise le port par défaut sans réglage", () => {
		expect(portFrom(undefined)).toBe(DEFAULT_PORT);
		expect(portFrom({})).toBe(DEFAULT_PORT);
		expect(urlFor({})).toBe("ws://127.0.0.1:8765/");
	});

	it.each([
		[9000, 9000],
		["9000", 9000],
		[" 9001 ", 9001],
		["", DEFAULT_PORT],
		["abc", DEFAULT_PORT],
		["80", DEFAULT_PORT],
		[70000, DEFAULT_PORT],
		[1023, DEFAULT_PORT],
		[1024, 1024],
		[65535, 65535],
		[8765.5, DEFAULT_PORT],
	])("port %j → %d", (input, expected) => {
		expect(portFrom({ port: input })).toBe(expected);
	});

	it("vise toujours la boucle locale", () => {
		expect(urlFor({ port: 9000 })).toBe("ws://127.0.0.1:9000/");
	});
});
