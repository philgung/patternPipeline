using FluentResults;
using Microsoft.Extensions.Logging;

namespace PatternPipeline
{
    public record Pipeline<TOut>
    {
        private readonly ILogger _logger;
        private readonly Result<TOut> _source;

        public Pipeline(ILogger logger, Result<TOut> source = default)
        {
            _logger = logger;
            _source = source;
        }

        public Pipeline<TOut> Configurer<TOut>(
            Func<SourceDePipeline, EtapeDePipeline<TOut>> build)
        {
            var sourcePipeline = new SourceDePipeline(_logger);
            var etape = build(sourcePipeline);
            if (LaConfigurationEstValide(etape))
            {
                return new Pipeline<TOut>(_logger, etape.Source);
            }
            return new Pipeline<TOut>(_logger, Result.Fail(
                $"Le type de retour '{etape.GetType().GenericTypeArguments.Last()}' n'est pas valide."));
        }

        public Result<TOut> Executer() => _source;

        private bool LaConfigurationEstValide<TOut>(EtapeDePipeline<TOut> etape) =>
            GetType().GenericTypeArguments.Last()
                .IsAssignableFrom(etape.GetType().GenericTypeArguments.Last());

        public record EtapeDePipeline<TIn>(Result<TIn> Source, ILogger Logger)
        {
            public EtapeDePipeline<TOut> Ajouter<TOut>(IPipe<TIn, TOut> pipe)
            {
                if (Source.IsFailed)
                {
                    return new EtapeDePipeline<TOut>(
                        Result.Fail(Source.Errors.Select(_ => _.Message)),
                        Logger);
                }
                TOut resultat;
                try
                {
                    resultat = pipe.Executer(Source.Value);
                }
                catch (Exception exception)
                {
                    if (Source.Value is TOut sourceValue)
                    {
                        Logger.LogError(exception.Message);
                        return new EtapeDePipeline<TOut>(sourceValue, Logger);
                    }
                    return new EtapeDePipeline<TOut>(
                        Result.Fail(new Error(exception.Message)),
                        Logger);
                }
                
                return new(resultat, Logger);
            }

            public EtapeDePipeline<TIn> AjouterLog(Func<TIn, string> genererInformation)
            {
                if (Source.IsSuccess)
                {
                    Logger.LogInformation(genererInformation(Source.Value));
                }
                return new EtapeDePipeline<TIn>(Source, Logger);
            }

            public EtapeDePipeline<TIn> AjouterLogDErreur(Func<string, string> genererInformation)
            {
                if (Source.IsFailed)
                {
                    Logger.LogError(genererInformation(string.Join('/', Source.Errors.Select(_ => _.Message))));
                }
                return new EtapeDePipeline<TIn>(Source, Logger);
            }
        }

        public record SourceDePipeline(ILogger Logger)
        {
            public EtapeDePipeline<TOut> Ajouter<TOut>(IPipe<TOut,TOut> pipe)
            {
                TOut resultat;
                try
                {
                    resultat = pipe.Executer(default);
                }
                catch (Exception exception)
                {
                    return new EtapeDePipeline<TOut>(
                        Result.Fail(new Error(exception.Message)),
                        Logger);
                }
                
                return new(resultat, Logger);
            }
        }
    }
}