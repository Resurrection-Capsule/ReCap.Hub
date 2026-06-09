using System.Threading.Tasks;

namespace ReCap.Hub.ViewModels
{
    public class OkDialogViewModel
        : MessageDialogViewModelBase<bool>
    {
        public void OkCommand(object parameter)
            => CompletionSource.TrySetResult(true);

        public OkDialogViewModel(string title, string content, bool isCloseable = true)
            : base(title, content, isCloseable)
        { }
    }
}
