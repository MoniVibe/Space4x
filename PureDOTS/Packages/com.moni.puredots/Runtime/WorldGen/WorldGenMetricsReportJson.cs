using System.Text;

namespace PureDOTS.Runtime.WorldGen
{
    public static class WorldGenMetricsReportJson
    {
        public static string Serialize(string scenarioId, in WorldGenMetrics metrics)
        {
            var builder = new StringBuilder(256);
            builder.Append('{');
            builder.Append("\"scenario\":\"");
            builder.Append(string.IsNullOrWhiteSpace(scenarioId) ? "" : scenarioId);
            builder.Append("\",");
            builder.Append("\"inputHash32\":");
            builder.Append(metrics.InputHash32);
            builder.Append(',');
            builder.Append("\"outputHash64Lo\":");
            builder.Append(metrics.OutputHash64Lo);
            builder.Append(',');
            builder.Append("\"outputHash64Hi\":");
            builder.Append(metrics.OutputHash64Hi);
            builder.Append(',');
            builder.Append("\"counts\":{");
            builder.Append("\"food\":");
            builder.Append(metrics.FoodCount);
            builder.Append(',');
            builder.Append("\"wood\":");
            builder.Append(metrics.WoodCount);
            builder.Append(',');
            builder.Append("\"stone\":");
            builder.Append(metrics.StoneCount);
            builder.Append(',');
            builder.Append("\"villages\":");
            builder.Append(metrics.VillageCount);
            builder.Append("},");
            builder.Append("\"villageNNcm\":{");
            builder.Append("\"min\":");
            builder.Append(metrics.VillageNNMinCm);
            builder.Append(',');
            builder.Append("\"median\":");
            builder.Append(metrics.VillageNNMedianCm);
            builder.Append(',');
            builder.Append("\"max\":");
            builder.Append(metrics.VillageNNMaxCm);
            builder.Append("},");
            builder.Append("\"bootstrapCoverageQ\":");
            builder.Append(metrics.BootstrapCoverageQ);
            builder.Append('}');
            return builder.ToString();
        }
    }
}
