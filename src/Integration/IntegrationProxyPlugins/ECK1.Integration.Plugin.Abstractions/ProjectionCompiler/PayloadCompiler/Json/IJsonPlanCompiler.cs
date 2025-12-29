using Microsoft.Extensions.Configuration;

namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.PayloadCompiler.Json
{
    public interface IJsonPlanCompiler<TEvent, TRecord>
    {
        JsonExecutionPlan<TEvent, TRecord> Compile(string format, IConfigurationSection fieldsSection);
        void CompileArrayField<TItem>(IConfigurationSection field, List<JsonOp<TEvent, TRecord>> ops);
        void CompileArrayFieldForItem<TItem, TChildItem>(IConfigurationSection field, List<JsonOp<TEvent, TRecord>> ops);
    }
}