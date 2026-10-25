import { defineConfig } from "vitest/config";

export default defineConfig({
	test: {
		include: ["tests/**/*.test.ts"],
		environment: "node",
		// Les tests réseau utilisent de vrais sockets locaux : on évite les exécutions parallèles entre fichiers.
		fileParallelism: false,
	},
});
