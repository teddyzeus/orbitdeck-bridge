import { SingletonAction, type DidReceiveSettingsEvent, type KeyAction, type WillAppearEvent, type WillDisappearEvent } from "@elgato/streamdeck";
import type { JsonObject } from "@elgato/utils";

import type { ModClient } from "../connection/ModClient.js";
import { resolveLocale, stringsFor, type Locale } from "../i18n/strings.js";
import type { FaceContext } from "../rendering/faces.js";
import { renderFace, type KeyFace } from "../rendering/renderFace.js";
import type { Store } from "../state/Store.js";

/** Intervalle minimal entre deux redessins : le mod émet jusqu'à 10 états par seconde, les touches n'ont pas besoin de plus de 5. */
const REFRESH_INTERVAL_MS = 200;

export interface DisplayDependencies {
	store: Store;
	client: ModClient;
	/** Langue de l'interface Stream Deck (`fr`, `en`…). */
	language: string | undefined;
}

/**
 * Base des touches qui affichent une valeur du jeu. Redessine toutes ses instances visibles quand l'état change,
 * mais seulement si l'image obtenue diffère de la précédente (pas de trafic inutile vers le Stream Deck).
 */
export abstract class DisplayAction<TSettings extends JsonObject = JsonObject> extends SingletonAction<TSettings> {
	protected readonly store: Store;
	protected readonly client: ModClient;
	protected readonly locale: Locale;

	readonly #settings = new Map<string, TSettings>();
	readonly #lastImage = new Map<string, string>();
	#refreshTimer: ReturnType<typeof setTimeout> | undefined;
	#lastRefresh = 0;

	constructor(dependencies: DisplayDependencies) {
		super();
		this.store = dependencies.store;
		this.client = dependencies.client;
		this.locale = resolveLocale(dependencies.language);
		this.store.subscribe(() => this.scheduleRefresh());
	}

	/** Contenu de la touche pour l'état courant. */
	protected abstract face(context: FaceContext, settings: TSettings): KeyFace;

	protected get faceContext(): FaceContext {
		return {
			availability: this.store.availability,
			state: this.store.state,
			strings: stringsFor(this.locale),
			locale: this.locale,
		};
	}

	/** Réglages mémorisés d'une instance de la touche. */
	protected settingsOf(actionId: string): TSettings {
		return this.#settings.get(actionId) ?? ({} as TSettings);
	}

	override async onWillAppear(ev: WillAppearEvent<TSettings>): Promise<void> {
		this.#settings.set(ev.action.id, ev.payload.settings);
		if (ev.action.isKey()) {
			await ev.action.setTitle("");
			await this.draw(ev.action, true);
		}
	}

	override onWillDisappear(ev: WillDisappearEvent<TSettings>): void {
		this.#settings.delete(ev.action.id);
		this.#lastImage.delete(ev.action.id);
	}

	override async onDidReceiveSettings(ev: DidReceiveSettingsEvent<TSettings>): Promise<void> {
		this.#settings.set(ev.action.id, ev.payload.settings);
		if (ev.action.isKey()) {
			await this.draw(ev.action, true);
		}
	}

	/** Mémorise de nouveaux réglages (et les persiste) puis redessine la touche. */
	protected async updateSettings(action: KeyAction<TSettings>, settings: TSettings): Promise<void> {
		this.#settings.set(action.id, settings);
		await action.setSettings(settings);
		await this.draw(action, true);
	}

	protected async draw(action: KeyAction<TSettings>, force = false): Promise<void> {
		const image = renderFace(this.face(this.faceContext, this.settingsOf(action.id)));
		if (!force && this.#lastImage.get(action.id) === image) {
			return;
		}

		this.#lastImage.set(action.id, image);
		await action.setImage(image);
	}

	protected scheduleRefresh(): void {
		if (this.#refreshTimer !== undefined) {
			return;
		}

		const wait = Math.max(0, REFRESH_INTERVAL_MS - (Date.now() - this.#lastRefresh));
		this.#refreshTimer = setTimeout(() => {
			this.#refreshTimer = undefined;
			this.#lastRefresh = Date.now();
			this.refreshAll();
		}, wait);
	}

	protected refreshAll(): void {
		for (const action of this.actions.toArray()) {
			if (action.isKey()) {
				void this.draw(action).catch(() => undefined);
			}
		}
	}
}
