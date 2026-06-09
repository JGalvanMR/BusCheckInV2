using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;

namespace BusCheckInV2.ViewModels
{
    public abstract partial class BaseViewModel : ObservableObject, IDisposable
    {
        protected readonly CancellationTokenSource _cancellationTokenSource = new();

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }
        }
    }
}