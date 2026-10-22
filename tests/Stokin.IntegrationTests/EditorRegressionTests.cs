using System.Reflection;
using Stokin.Application;
using Stokin.Application.Common;
using Stokin.Desktop.ViewModels;
using Stokin.Testing;
namespace Stokin.IntegrationTests;

public class EditorRegressionTests
{
    [Test]
    public async Task Shared_busy_guard_blocks_mutation_and_overlapping_save_post()
    {
        var (vm, proxy) = Create(); vm.SaveCommand.Execute(null); Assert.True(vm.IsBusy); Assert.True(!vm.CanEdit, "Mutations must be disabled while SQL save awaits."); vm.PostCommand.Execute(null); Assert.Equal(1, proxy.Saves); proxy.Completion.SetResult(Result()); await Task.Delay(30);
    }
    [Test]
    public async Task Delayed_save_does_not_mark_newer_edits_as_saved()
    {
        var (vm, proxy) = Create(); vm.SaveCommand.Execute(null); vm.Lines[0].Quantity = "2"; proxy.Completion.SetResult(Result()); for (int i = 0; i < 100 && vm.IsBusy; i++) await Task.Delay(5); Assert.True(vm.Dirty, "An edit made after the save snapshot must still trigger unsaved warning.");
    }
    static (DocumentEditorViewModel, DelayedRepository) Create()
    {
        var repository = DispatchProxy.Create<IStokinRepository, DelayedRepository>(); var proxy = (DelayedRepository)(object)repository; var vm = new DocumentEditorViewModel(new(repository), () => new(Guid.NewGuid(), 1), "IN"); vm.Lines.Add(new() { ProductId = 1, Quantity = "1", Cost = "100", Name = "Barang", Unit = "pcs" }); return (vm, proxy);
    }
    static DocumentView Result() => new(1, "IN-1", "IN", "DRAFT", 1, "", null, null, [1], [new(1, 1, 100)], new());
    public class DelayedRepository : DispatchProxy
    {
        public int Saves { get; private set; }
        public TaskCompletionSource<DocumentView> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IStokinRepository.SaveDraftAsync)) { Saves++; return Completion.Task; }
            throw new NotSupportedException(method.Name);
        }
    }
}
