import { action, type KeyDownEvent } from "@elgato/streamdeck";

import { statusFace, type FaceContext } from "../rendering/faces.js";
import type { KeyFace } from "../rendering/renderFace.js";
import { DisplayAction } from "./DisplayAction.js";

/** État de la connexion au jeu. Un appui force une nouvelle tentative de connexion immédiate. */
@action({ UUID: "com.teddyzeus.streamplug.status" })
export class StatusAction extends DisplayAction {
	protected override face(context: FaceContext): KeyFace {
		return statusFace(context);
	}

	override async onKeyDown(_ev: KeyDownEvent): Promise<void> {
		if (this.store.availability === "offline") {
			this.client.reconnectNow();
		}
	}
}
