namespace PatternPipeline
{
    public interface IPipe<TIn, TOut>
    {
        TOut Executer(TIn entree);
    }
}