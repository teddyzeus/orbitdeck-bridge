import { describe, expect, it } from "vitest";

import { numberSetting } from "../src/actions/settings.js";

describe("numberSetting", () => {
	it("accepte un nombre ou une chaîne numérique", () => {
		expect(numberSetting(50, 0, 0, 100)).toBe(50);
		expect(numberSetting("25", 0, 0, 100)).toBe(25);
	});

	it("revient à la valeur par défaut si absent, invalide ou hors bornes", () => {
		expect(numberSetting(undefined, 10, 1, 100)).toBe(10);
		expect(numberSetting("", 10, 1, 100)).toBe(10);
		expect(numberSetting("abc", 10, 1, 100)).toBe(10);
		expect(numberSetting(0, 10, 1, 100)).toBe(10);
		expect(numberSetting(500, 10, 1, 100)).toBe(10);
		expect(numberSetting(Number.NaN, 10, 1, 100)).toBe(10);
	});

	it("accepte 0 quand les bornes le permettent", () => {
		expect(numberSetting("0", 50, 0, 100)).toBe(0);
	});
});
