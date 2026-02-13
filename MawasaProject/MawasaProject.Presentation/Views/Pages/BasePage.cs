using Microsoft.Extensions.DependencyInjection;

namespace MawasaProject.Presentation.Views.Pages;

public abstract class BasePage<TViewModel> : ContentPage where TViewModel : class
{
    protected BasePage()
    {
        BindingContext = App.Services.GetRequiredService<TViewModel>();
    }

    protected TViewModel ViewModel => (TViewModel)BindingContext;
}
