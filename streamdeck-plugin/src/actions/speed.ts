import { action, type KeyDownEvent } from "@elgato/streamdeck";

import { nextSpeedMode, speedFace, type FaceContext } from "../rendering/faces.js";
import type { KeyFace } from "../rendering/renderFace.js";
import { DisplayAction } from "./DisplayAction.js";

/**
 * Vitesse du véhicule, du type affiché par le jeu (surface par défaut). Un appui demande au jeu de passer au type
 * suivant (surface → orbite → cible) : la touche suit le navball, comme si le joueur avait cliqué dessus.
 */
@action({ UUID: "com.teddyzeus.streamplug.speed" })
export class SpeedAction extends DisplayAction {
	protected override face(context: FaceContext): KeyFace {
		return speedFace(context);
	}

	override async onKeyDown(ev: KeyDownEvent): Promise<void> {
		const next = nextSpeedMode(this.store.state);
		if (next === undefined || this.store.availability === "offline") {
			await ev.action.showAlert();
			return;
		}

		await this.command(ev.action, "setSpeedMode", { mode: next });
	}
}
