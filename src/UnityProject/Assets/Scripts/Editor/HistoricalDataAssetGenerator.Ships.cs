using System.Collections.Generic;
using Pacifico.Core.Naval;
using Pacifico.Data;

namespace Pacifico.EditorTools
{
    public static partial class HistoricalDataAssetGenerator
    {
        private static void GenerateShips(List<string> report)
        {
            foreach (ShipSpec spec in ShipCatalog.All())
            {
                var asset = LoadOrCreate<ShipDataSO>(ProjectPaths.ShipData, "Ship_" + spec.Id);
                asset.CopyFrom(spec);
                Finish(asset, spec.Validate().IsValid, report);
            }
        }
    }
}
