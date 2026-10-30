using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Protocol
{
    /// <summary>Codes d'erreur du protocole (voir protocol/README.md).</summary>
    public static class ErrorCodes
    {
        public const string BadRequest = "BAD_REQUEST";
        public const string UnknownCommand = "UNKNOWN_COMMAND";
        public const string NoVessel = "NO_VESSEL";
        public const string NotControllable = "NOT_CONTROLLABLE";
        public const string RejectedByGame = "REJECTED_BY_GAME";
        public const string Internal = "INTERNAL";
    }

    /// <summary>Construction des messages serveur → client (protocole v1). Ne dépend d'aucun type du jeu.</summary>
    public static class ProtocolMessages
    {
        public const int Version = 1;

        public static string Hello(string modVersion, string gameVersion) =>
            Serialize(Base("hello", new JObject
            {
                ["mod"] = modVersion ?? string.Empty,
                ["game"] = gameVersion ?? string.Empty
            }));

        public static string State(TelemetrySnapshot snapshot, long sequence)
        {
            var message = Base("state", new JObject
            {
                ["seq"] = sequence,
                ["status"] = StatusName(snapshot.Status),
                ["gameState"] = snapshot.GameStateName
            });

            if (snapshot.Status != VesselStatus.NoVessel)
            {
                message["vessel"] = new JObject { ["name"] = snapshot.VesselName };
                message["altitude"] = new JObject
                {
                    ["seaLevel"] = Number(snapshot.AltitudeSeaLevel, 2),
                    ["surface"] = Number(snapshot.AltitudeSurface, 2)
                };
                message["speed"] = new JObject
                {
                    ["mode"] = SpeedModeName(snapshot.SpeedMode),
                    ["surface"] = Number(snapshot.SpeedSurface, 2),
                    ["orbital"] = Number(snapshot.SpeedOrbital, 2),
                    ["target"] = snapshot.SpeedTarget.HasValue ? Number(snapshot.SpeedTarget.Value, 2) : JValue.CreateNull()
                };
                message["throttle"] = Math.Round(Math.Min(1f, Math.Max(0f, snapshot.Throttle)), 3);
                var sas = new JObject
                {
                    ["enabled"] = snapshot.SasEnabled,
                    ["mode"] = snapshot.SasMode
                };
                if (snapshot.AvailableSasModes != null)
                {
                    var available = new JArray();
                    foreach (var mode in snapshot.AvailableSasModes)
                        available.Add(SasModeName(mode));
                    sas["available"] = available;
                }

                message["sas"] = sas;
                message["rcs"] = new JObject { ["enabled"] = snapshot.RcsEnabled };

                // Champs ajoutés après la v1 initiale : absents quand le jeu ne les fournit pas.
                if (snapshot.LightsEnabled.HasValue)
                    message["lights"] = new JObject { ["enabled"] = snapshot.LightsEnabled.Value };
                if (snapshot.BrakesEnabled.HasValue)
                    message["brakes"] = new JObject { ["enabled"] = snapshot.BrakesEnabled.Value };
                if (snapshot.GearEnabled.HasValue)
                    message["gear"] = new JObject { ["enabled"] = snapshot.GearEnabled.Value };
                if (snapshot.View.HasValue)
                    message["view"] = snapshot.View.Value == ViewKind.Map ? "map" : "ship";
                if (snapshot.StageCount.HasValue)
                {
                    var stage = new JObject { ["count"] = snapshot.StageCount.Value };
                    if (snapshot.StageExhausted.HasValue)
                        stage["exhausted"] = snapshot.StageExhausted.Value;
                    message["stage"] = stage;
                }

                if (snapshot.Time != null)
                    message["time"] = TimeObject(snapshot.Time);
                if (snapshot.Maneuver != null)
                    message["maneuver"] = ManeuverObject(snapshot.Maneuver);
            }

            return Serialize(message);
        }

        /// <summary>Objet « time » de l'état : paliers, pause et instant de jeu. Les champs inconnus sont omis, jamais écrits à 0.</summary>
        private static JObject TimeObject(TimeInfo time)
        {
            var rates = new JArray();
            foreach (var rate in time.Rates)
                rates.Add(Math.Round(rate, 3));

            var body = new JObject
            {
                ["ut"] = Number(time.Ut, 1),
                ["rate"] = Number(time.Rate, 3),
                ["index"] = time.Index,
                ["rates"] = rates,
                ["mode"] = time.Mode == WarpMode.Physics ? "physics" : "rails",
                ["paused"] = time.Paused
            };

            if (time.Pausable)
                body["pausable"] = true;
            if (time.MaxIndex.HasValue)
                body["maxIndex"] = time.MaxIndex.Value;
            if (time.Limit.HasValue)
                body["limit"] = WarpLimitName(time.Limit.Value);
            if (time.Auto)
                body["auto"] = true;
            if (time.DayLength.HasValue)
                body["day"] = Number(time.DayLength.Value, 0);

            return body;
        }

        /// <summary>Objet « maneuver » de l'état : nœuds (huit au plus), orbite actuelle, delta-V et combustion. Champs inconnus omis.</summary>
        private static JObject ManeuverObject(ManeuverInfo maneuver)
        {
            var nodes = new JArray();
            foreach (var node in maneuver.Nodes)
            {
                if (nodes.Count >= ManeuverInfo.MaxNodes)
                    break;

                var item = new JObject { ["ut"] = Number(node.Ut, 1), ["dv"] = Number(node.Dv, 1) };
                if (node.Burn.HasValue)
                    item["burn"] = Number(node.Burn.Value, 1);
                if (node.Ignition.HasValue)
                    item["ignition"] = Number(node.Ignition.Value, 1);
                if (node.After != null)
                    item["after"] = OrbitObject(node.After);
                nodes.Add(item);
            }

            var body = new JObject { ["count"] = maneuver.Count, ["nodes"] = nodes };
            if (maneuver.Before != null)
                body["before"] = OrbitObject(maneuver.Before);
            if (maneuver.DvAvailable.HasValue)
                body["dvAvailable"] = Number(maneuver.DvAvailable.Value, 1);
            if (maneuver.Burning != null)
            {
                var burning = new JObject();
                if (maneuver.Burning.Remaining.HasValue)
                    burning["remaining"] = Number(maneuver.Burning.Remaining.Value, 1);
                if (maneuver.Burning.Progress.HasValue)
                    burning["progress"] = Number(maneuver.Burning.Progress.Value, 3);
                body["burning"] = burning;
            }

            if (maneuver.Warp != null)
            {
                var warp = new JObject { ["state"] = WarpStateName(maneuver.Warp.State) };
                if (maneuver.Warp.Stop.HasValue)
                    warp["stop"] = Number(maneuver.Warp.Stop.Value, 1);
                if (maneuver.Warp.Reason.HasValue)
                    warp["reason"] = WarpReasonName(maneuver.Warp.Reason.Value);
                body["warp"] = warp;
            }

            return body;
        }

        private static string WarpStateName(ManeuverWarpState state)
        {
            switch (state)
            {
                case ManeuverWarpState.Running: return "running";
                case ManeuverWarpState.Arrived: return "arrived";
                case ManeuverWarpState.Late: return "late";
                case ManeuverWarpState.Interrupted: return "interrupted";
                default: return "idle";
            }
        }

        private static string WarpReasonName(ManeuverWarpReason reason)
        {
            switch (reason)
            {
                case ManeuverWarpReason.NodeGone: return "nodeGone";
                case ManeuverWarpReason.Vessel: return "vessel";
                case ManeuverWarpReason.Control: return "control";
                default: return "manual";
            }
        }

        private static JObject OrbitObject(OrbitInfo orbit)
        {
            var body = new JObject();
            if (orbit.Ap.HasValue)
                body["ap"] = Number(orbit.Ap.Value, 0);
            if (orbit.Pe.HasValue)
                body["pe"] = Number(orbit.Pe.Value, 0);
            if (orbit.Escape)
                body["escape"] = true;
            if (orbit.Atmosphere.HasValue)
                body["atmosphere"] = Number(orbit.Atmosphere.Value, 0);
            return body;
        }

        public static string WarpLimitName(WarpLimit limit)
        {
            switch (limit)
            {
                case WarpLimit.Atmosphere: return "atmosphere";
                case WarpLimit.Altitude: return "altitude";
                case WarpLimit.Thrust: return "thrust";
                case WarpLimit.Ground: return "ground";
                case WarpLimit.Proximity: return "proximity";
                default: return "other";
            }
        }

        public static string Ack(string id) => Serialize(Base("ack", new JObject { ["id"] = id }));

        public static string Error(string id, string code, string message)
        {
            var body = new JObject { ["code"] = code, ["message"] = message ?? string.Empty };
            if (id != null)
                body["id"] = id;
            return Serialize(Base("error", body));
        }

        public static string Pong() => Serialize(Base("pong", new JObject()));

        /// <summary>Nom d'un mode du SAS dans le protocole (le même que celui de la commande « setSasMode »).</summary>
        public static string SasModeName(SasModeKind mode)
        {
            switch (mode)
            {
                case SasModeKind.Prograde: return "prograde";
                case SasModeKind.Retrograde: return "retrograde";
                case SasModeKind.Normal: return "normal";
                case SasModeKind.Antinormal: return "antinormal";
                case SasModeKind.RadialIn: return "radialIn";
                case SasModeKind.RadialOut: return "radialOut";
                case SasModeKind.Target: return "target";
                case SasModeKind.AntiTarget: return "antiTarget";
                case SasModeKind.Maneuver: return "maneuver";
                default: return "stabilityAssist";
            }
        }

        public static string StatusName(VesselStatus status)
        {
            switch (status)
            {
                case VesselStatus.Connected: return "connected";
                case VesselStatus.NotControllable: return "notControllable";
                default: return "noVessel";
            }
        }

        public static string SpeedModeName(SpeedMode mode)
        {
            switch (mode)
            {
                case SpeedMode.Orbit: return "orbit";
                case SpeedMode.Target: return "target";
                default: return "surface";
            }
        }

        private static JObject Base(string type, JObject body)
        {
            var message = new JObject { ["v"] = Version, ["type"] = type };
            foreach (var property in body.Properties())
                message[property.Name] = property.Value;
            return message;
        }

        // NaN et l'infini ne sont pas du JSON valide : ils deviennent null.
        private static JToken Number(double value, int decimals) =>
            double.IsNaN(value) || double.IsInfinity(value) ? JValue.CreateNull() : new JValue(Math.Round(value, decimals));

        private static string Serialize(JObject message) => message.ToString(Formatting.None);
    }
}
