import { action, type KeyDownEvent } from "@elgato/streamdeck";

import { altitudeFace, nextAltitudeMode, type AltitudeMode, type FaceContext } from "../rendering/faces.js";
import type { KeyFace } from "../rendering/renderFace.js";
import { DisplayAction } from "./DisplayAction.js";

export type AltitudeSettings = {
	/** `surface` : au-dessus du terrain (défaut) ; `sea` : au-dessus du niveau de la mer. */
	mode?: AltitudeMode;
};

/** Altitude du véhicule. Un appui bascule entre l'altitude au-dessus du sol et au-dessus du niveau de la mer. */
@action({ UUID: "com.teddyzeus.streamplug.altitude" })
export class AltitudeAction extends DisplayAction<AltitudeSettings> {
	protected override face(context: FaceContext, settings: AltitudeSettings): KeyFace {
		return altitudeFace(context, settings.mode ?? "surface");
	}

	override async onKeyDown(ev: KeyDownEvent<AltitudeSettings>): Promise<void> {
		if (!ev.action.isKey()) {
			return;
		}

		const current = this.settingsOf(ev.action.id).mode ?? ev.payload.settings.mode ?? "surface";
		await this.updateSettings(ev.action, { ...ev.payload.settings, mode: nextAltitudeMode(current) });
	}
}
