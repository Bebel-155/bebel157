using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BebelEquipe155
{
    public sealed class DriverResolver
    {
        static bool EqualsAny(string value, string[] values)
        {
            if (string.IsNullOrWhiteSpace(value) || values == null) return false;
            return values.Any(x => string.Equals((x ?? "").Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        static bool WildcardMatch(string value, string pattern)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern)) return false;
            string regex = "^" + Regex.Escape(pattern.Trim()).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            return Regex.IsMatch(value.Trim(), regex, RegexOptions.IgnoreCase);
        }

        static bool HardwareMatches(UsbDeviceSnapshot device, DriverPackage package)
        {
            if (device == null || package == null || package.HardwareIdPatterns == null) return false;
            IEnumerable<string> ids = (device.HardwareIds ?? new string[0]).Concat(device.CompatibleIds ?? new string[0]);
            if (!string.IsNullOrWhiteSpace(device.InstanceId)) ids = ids.Concat(new[] { device.InstanceId });
            foreach (string id in ids)
                foreach (string pattern in package.HardwareIdPatterns)
                    if (WildcardMatch(id, pattern)) return true;
            return false;
        }

        static bool ManufacturerMatches(UsbDeviceSnapshot device, DriverPackage package)
        {
            if (device == null || package == null || string.IsNullOrWhiteSpace(device.Manufacturer) || string.IsNullOrWhiteSpace(package.Manufacturer))
                return false;

            string a = device.Manufacturer.ToLowerInvariant();
            string b = package.Manufacturer.ToLowerInvariant();
            return a.Contains(b) || b.Contains(a);
        }

        public DriverRecommendation Resolve(UsbDeviceSnapshot device, IEnumerable<DriverPackage> candidates)
        {
            DriverRecommendation recommendation = new DriverRecommendation();
            if (device == null)
            {
                recommendation.CurrentState = UsbAdbState.Unknown;
                recommendation.Confidence = DriverConfidence.Generic;
                recommendation.Action = "Conecte um dispositivo USB.";
                recommendation.Reason = "Nenhum dispositivo compatível selecionado.";
                return recommendation;
            }

            string adb = (device.AdbStateRaw ?? "").Trim().ToLowerInvariant();
            if (adb == "device")
            {
                recommendation.CurrentState = UsbAdbState.AdbReady;
                recommendation.Action = "Nenhuma instalação necessária.";
                recommendation.Reason = "ADB reconheceu o dispositivo como autorizado.";
                return recommendation;
            }
            if (adb == "unauthorized")
            {
                recommendation.CurrentState = UsbAdbState.AdbUnauthorized;
                recommendation.Action = "Autorize a depuração USB na tela do aparelho.";
                recommendation.Reason = "O driver está funcional; falta autorização do usuário no Android.";
                return recommendation;
            }
            if (adb == "offline")
            {
                recommendation.CurrentState = UsbAdbState.AdbOffline;
                recommendation.Action = "Reinicie o ADB e verifique cabo/porta antes de reinstalar driver.";
                recommendation.Reason = "O transporte ADB existe, mas está offline.";
                return recommendation;
            }

            int problem = device.ProblemCode ?? 0;
            bool hasDriver = device.Driver != null &&
                             (!string.IsNullOrWhiteSpace(device.Driver.Provider) || !string.IsNullOrWhiteSpace(device.Driver.InfName));

            if (problem == 28 || !hasDriver)
            {
                recommendation.CurrentState = UsbAdbState.DriverMissing;
                recommendation.Action = "Instalar o driver recomendado.";
                recommendation.Reason = problem == 28 ? "Windows reporta driver ausente (Problem Code 28)." : "Nenhum driver associado foi encontrado.";
            }
            else if (problem != 0)
            {
                recommendation.CurrentState = UsbAdbState.DriverIncorrect;
                recommendation.Action = "Reparar/reinstalar o driver recomendado.";
                recommendation.Reason = "Windows reporta problema de driver/carga (código " + problem + ").";
            }
            else
            {
                recommendation.CurrentState = UsbAdbState.UsbOnly;
                recommendation.Action = "Verifique a interface ADB e a depuração USB.";
                recommendation.Reason = "USB detectado, mas nenhuma sessão ADB foi associada.";
            }

            List<DriverPackage> allowed = (candidates ?? Enumerable.Empty<DriverPackage>())
                .Where(x => x != null && x.RedistributionStatus == DriverRedistributionStatus.Allowed)
                .OrderBy(x => x.Priority)
                .ToList();

            DriverPackage match = allowed.FirstOrDefault(x => HardwareMatches(device, x));
            if (match != null)
            {
                recommendation.PackageId = match.Id;
                recommendation.Confidence = DriverConfidence.Exact;
                recommendation.Reason += " Hardware ID corresponde ao pacote " + match.DisplayName + ".";
                return recommendation;
            }

            match = allowed.FirstOrDefault(x => EqualsAny(device.VendorId, x.UsbVendorIds));
            if (match != null)
            {
                recommendation.PackageId = match.Id;
                recommendation.Confidence = DriverConfidence.Probable;
                recommendation.Reason += " VID " + device.VendorId + " corresponde ao fabricante do pacote.";
                return recommendation;
            }

            match = allowed.FirstOrDefault(x => ManufacturerMatches(device, x));
            if (match != null)
            {
                recommendation.PackageId = match.Id;
                recommendation.Confidence = DriverConfidence.Probable;
                recommendation.Reason += " Fabricante USB corresponde ao pacote.";
                return recommendation;
            }

            match = allowed.FirstOrDefault(x => string.Equals(x.Manufacturer, "Generic", StringComparison.OrdinalIgnoreCase) ||
                                                string.Equals(x.Manufacturer, "Google", StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                recommendation.PackageId = match.Id;
                recommendation.Confidence = DriverConfidence.Generic;
                recommendation.Reason += " Somente fallback genérico está disponível.";
            }
            else
            {
                recommendation.Confidence = DriverConfidence.Generic;
                recommendation.Reason += " Nenhum pacote offline aprovado corresponde a este hardware.";
            }

            return recommendation;
        }
    }
}
