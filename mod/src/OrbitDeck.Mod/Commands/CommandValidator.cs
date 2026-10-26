using System;
using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Commands
{
    /// <summary>Résultat de la validation d'une commande reçue : une commande exécutable, ou une erreur à renvoyer.</summary>
    public sealed class ValidationResult
    {
        public bool IsValid => ErrorCode == null;

        public GameCommand Command { get; private set; }

        public string ErrorCode { get; private set; }

        public string ErrorMessage { get; private set; }

        public static ValidationResult Valid(GameCommand command) => new ValidationResult { Command = command };

        public static ValidationResult Invalid(string code, string message) =>
            new ValidationResult { ErrorCode = code, ErrorMessage = message };
    }

    /// <summary>
    /// Valide le nom et les arguments d'une commande (protocole v1). Les valeurs hors bornes sont refusées,
    /// jamais corrigées en silence : une poussée de 5 ne doit pas devenir 100 %.
    /// </summary>
    public static class CommandValidator
    {
        public static ValidationResult Validate(string name, JObject args)
        {
            args = args ?? new JObject();

            switch (name)
            {
                case "setThrottle":
                    return TryNumber(args, "value", 0, 1, out var value)
                        ? Build(CommandKind.SetThrottle, c => c.Amount = value)
                        : BadArgument("args.value doit être un nombre entre 0 et 1.");

                case "adjustThrottle":
                    return TryNumber(args, "delta", -1, 1, out var delta)
                        ? Build(CommandKind.AdjustThrottle, c => c.Amount = delta)
                        : BadArgument("args.delta doit être un nombre entre -1 et 1.");

                case "setSas":
                    return TryBoolean(args, "enabled", out var sas)
                        ? Build(CommandKind.SetSas, c => c.Enabled = sas)
                        : BadArgument("args.enabled doit être un booléen.");

                case "toggleSas":
                    return Build(CommandKind.ToggleSas, null);

                case "setRcs":
                    return TryBoolean(args, "enabled", out var rcs)
                        ? Build(CommandKind.SetRcs, c => c.Enabled = rcs)
                        : BadArgument("args.enabled doit être un booléen.");

                case "toggleRcs":
                    return Build(CommandKind.ToggleRcs, null);

                case "setSpeedMode":
                    return TryParseSpeedMode(args, out var mode)
                        ? Build(CommandKind.SetSpeedMode, c => c.Mode = mode)
                        : BadArgument("args.mode doit valoir « surface », « orbit » ou « target ».");

                case "setSasMode":
                    return TryParseSasMode(args, out var sasMode)
                        ? Build(CommandKind.SetSasMode, c => c.SasMode = sasMode)
                        : BadArgument("args.mode doit valoir « stabilityAssist », « prograde », « retrograde », « normal », « antinormal », « radialIn », « radialOut », « target », « antiTarget » ou « maneuver ».");

                case "setLights":
                    return TryBoolean(args, "enabled", out var lights)
                        ? Build(CommandKind.SetLights, c => c.Enabled = lights)
                        : BadArgument("args.enabled doit être un booléen.");

                case "toggleLights":
                    return Build(CommandKind.ToggleLights, null);

                case "setBrakes":
                    return TryBoolean(args, "enabled", out var brakes)
                        ? Build(CommandKind.SetBrakes, c => c.Enabled = brakes)
                        : BadArgument("args.enabled doit être un booléen.");

                case "toggleBrakes":
                    return Build(CommandKind.ToggleBrakes, null);

                case "setGear":
                    return TryBoolean(args, "enabled", out var gear)
                        ? Build(CommandKind.SetGear, c => c.Enabled = gear)
                        : BadArgument("args.enabled doit être un booléen.");

                case "toggleGear":
                    return Build(CommandKind.ToggleGear, null);

                default:
                    return ValidationResult.Invalid(ErrorCodes.UnknownCommand, $"Commande « {Truncate(name)} » inconnue.");
            }
        }

        private static ValidationResult Build(CommandKind kind, Action<GameCommand> configure)
        {
            var command = new GameCommand { Kind = kind };
            configure?.Invoke(command);
            return ValidationResult.Valid(command);
        }

        private static ValidationResult BadArgument(string message) =>
            ValidationResult.Invalid(ErrorCodes.BadRequest, message);

        private static bool TryNumber(JObject args, string key, double min, double max, out float value)
        {
            value = 0;
            var token = args[key];
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                return false;

            var number = token.Value<double>();
            if (double.IsNaN(number) || double.IsInfinity(number) || number < min || number > max)
                return false;

            value = (float)number;
            return true;
        }

        private static bool TryBoolean(JObject args, string key, out bool value)
        {
            value = false;
            var token = args[key];
            if (token == null || token.Type != JTokenType.Boolean)
                return false;

            value = token.Value<bool>();
            return true;
        }

        private static bool TryParseSpeedMode(JObject args, out SpeedMode mode)
        {
            mode = SpeedMode.Surface;
            var token = args["mode"];
            if (token == null || token.Type != JTokenType.String)
                return false;

            switch (token.Value<string>())
            {
                case "surface": mode = SpeedMode.Surface; return true;
                case "orbit": mode = SpeedMode.Orbit; return true;
                case "target": mode = SpeedMode.Target; return true;
                default: return false;
            }
        }

        private static bool TryParseSasMode(JObject args, out SasModeKind mode)
        {
            mode = SasModeKind.StabilityAssist;
            var token = args["mode"];
            if (token == null || token.Type != JTokenType.String)
                return false;

            switch (token.Value<string>())
            {
                case "stabilityAssist": mode = SasModeKind.StabilityAssist; return true;
                case "prograde": mode = SasModeKind.Prograde; return true;
                case "retrograde": mode = SasModeKind.Retrograde; return true;
                case "normal": mode = SasModeKind.Normal; return true;
                case "antinormal": mode = SasModeKind.Antinormal; return true;
                case "radialIn": mode = SasModeKind.RadialIn; return true;
                case "radialOut": mode = SasModeKind.RadialOut; return true;
                case "target": mode = SasModeKind.Target; return true;
                case "antiTarget": mode = SasModeKind.AntiTarget; return true;
                case "maneuver": mode = SasModeKind.Maneuver; return true;
                default: return false;
            }
        }

        private static string Truncate(string value) =>
            value == null ? string.Empty : value.Length <= 32 ? value : value.Substring(0, 32) + "…";
    }
}
