using ConcurrentBooking.WpfClient.Services;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace ConcurrentBooking.WpfClient;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        CreateSlotDoctorComboBox.SelectedIndex = 0;
        CreateSlotDatePicker.SelectedDate = DateTime.Today.AddDays(1);
        CreateSlotTimeComboBox.SelectedIndex = 0;

        CustomerIdTextBox.Text = Guid.NewGuid().ToString();
        IdempotencyKeyTextBox.Text = Guid.NewGuid().ToString("N");
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(LoadSlotsAsync);
    }

    private async void CreateSlotButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (CreateSlotDoctorComboBox.SelectedItem is not ComboBoxItem doctorItem)
        {
            WriteResult("Selecione o médico/recurso.");
            return;
        }

        if (!CreateSlotDatePicker.SelectedDate.HasValue)
        {
            WriteResult("Selecione a data do slot.");
            return;
        }

        if (CreateSlotTimeComboBox.SelectedItem is not ComboBoxItem timeItem ||
            !TimeSpan.TryParse(timeItem.Content?.ToString(), out var time))
        {
            WriteResult("Selecione um horário válido.");
            return;
        }

        if (!TryCreateClient(out var client))
            return;

        var startsAt = CreateSlotDatePicker.SelectedDate.Value.Date.Add(time);
        var endsAt = startsAt.AddMinutes(30);
        var doctorName = doctorItem.Content?.ToString() ?? string.Empty;
        var seatCode = string.IsNullOrWhiteSpace(CreateSlotSeatCodeTextBox.Text) ? null : CreateSlotSeatCodeTextBox.Text.Trim();

        await ExecuteAsync(async cancellationToken =>
        {
            var result = await client.CreateSlotAsync(doctorName, startsAt, endsAt, seatCode, cancellationToken);
            if (!result.IsSuccess || result.Data is null)
            {
                WriteResult($"Erro ao criar slot: {result.Error}");
                return;
            }

            SlotIdTextBox.Text = result.Data.Id.ToString();
            await LoadSlotsAsync(cancellationToken);
            WriteResult($"Slot criado com sucesso. SlotId: {result.Data.Id}");
        });
    }

    private async void RefreshSlotsButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(LoadSlotsAsync);
    }

    private void SlotsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SlotsDataGrid.SelectedItem is SlotDto slot)
        {
            SlotIdTextBox.Text = slot.Id.ToString();
        }
    }

    private async void CreateHoldButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadGuid(SlotIdTextBox.Text, "SlotId", out var slotId) ||
            !TryReadGuid(CustomerIdTextBox.Text, "CustomerId", out var customerId) ||
            !TryReadRequired(IdempotencyKeyTextBox.Text, "IdempotencyKey", out var idempotencyKey) ||
            !TryCreateClient(out var client))
        {
            return;
        }

        await ExecuteAsync(async cancellationToken =>
        {
            var result = await client.CreateHoldAsync(slotId, customerId, idempotencyKey, cancellationToken);
            if (result.IsSuccess && result.Data is not null)
            {
                HoldIdTextBox.Text = result.Data.HoldId.ToString();
                WriteResult($"Hold criado com sucesso.\nHoldId: {result.Data.HoldId}\nExpira em: {result.Data.ExpiresAt:yyyy-MM-dd HH:mm:ss}");
                return;
            }

            WriteResult($"Erro ao criar hold: {result.Error}");
        });
    }

    private async void ConfirmHoldButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadGuid(HoldIdTextBox.Text, "HoldId", out var holdId) ||
            !TryReadGuid(CustomerIdTextBox.Text, "CustomerId", out var customerId) ||
            !TryReadRequired(IdempotencyKeyTextBox.Text, "IdempotencyKey", out var idempotencyKey) ||
            !TryCreateClient(out var client))
        {
            return;
        }

        await ExecuteAsync(async cancellationToken =>
        {
            var result = await client.ConfirmHoldAsync(holdId, customerId, idempotencyKey, cancellationToken);
            if (result.IsSuccess && result.Data is not null)
            {
                WriteResult($"Booking confirmado com sucesso.\nBookingId: {result.Data.BookingId}");
                return;
            }

            WriteResult($"Erro ao confirmar hold: {result.Error}");
        });
    }

    private async Task LoadSlotsAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateClient(out var client))
            return;

        var result = await client.GetSlotsAsync(cancellationToken);
        if (!result.IsSuccess || result.Data is null)
        {
            WriteResult($"Erro ao carregar slots: {result.Error}");
            return;
        }

        SlotsDataGrid.ItemsSource = result.Data;
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> operation)
    {
        ToggleUi(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await operation(cts.Token);
        }
        catch (OperationCanceledException)
        {
            WriteResult("A operação excedeu o tempo limite de 15 segundos.");
        }
        catch (HttpRequestException ex)
        {
            WriteResult($"Erro de conexão com a API: {ex.Message}");
        }
        finally
        {
            ToggleUi(true);
        }
    }

    private bool TryCreateClient(out BookingApiClient client)
    {
        client = null!;

        if (!Uri.TryCreate(ApiBaseUrlTextBox.Text?.Trim(), UriKind.Absolute, out var baseUri))
        {
            WriteResult("URL base da API inválida.");
            return false;
        }

        var httpClient = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = Timeout.InfiniteTimeSpan
        };

        client = new BookingApiClient(httpClient);
        return true;
    }

    private bool TryReadGuid(string? value, string fieldName, out Guid parsed)
    {
        if (Guid.TryParse(value, out parsed))
            return true;

        WriteResult($"Campo inválido: {fieldName} deve ser um GUID válido.");
        return false;
    }

    private bool TryReadRequired(string? value, string fieldName, out string parsed)
    {
        parsed = value?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(parsed))
            return true;

        WriteResult($"Campo obrigatório: {fieldName}.");
        return false;
    }

    private void ToggleUi(bool enabled)
    {
        CreateSlotButton.IsEnabled = enabled;
        RefreshSlotsButton.IsEnabled = enabled;
        CreateHoldButton.IsEnabled = enabled;
        ConfirmHoldButton.IsEnabled = enabled;
    }

    private void WriteResult(string message)
    {
        ResultTextBox.Text = message;
    }
}