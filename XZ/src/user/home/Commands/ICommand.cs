public interface ICommand
    {
        string Name { get; }
        Task<string> Execute(string[] args);
    }