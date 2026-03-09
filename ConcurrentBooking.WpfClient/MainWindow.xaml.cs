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

        AvailabilityDatePicker.SelectedDate = DateTime.Today.AddDays(1);
        PeriodComboBox.ItemsSource = Enum.GetValues<AvailabilityPeriod>();
        PeriodComboBox.SelectedIndex = 0;

        CustomerIdTextBox.Text = Guid.NewGuid().ToString();
        IdempotencyKeyTextBox.Text = Guid.NewGuid().ToString("N");
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ExecuteAsync(LoadReferenceDataAsync);
    }

    private async void SearchProfessionalsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SpecialtyComboBox.SelectedItem is not SpecialtyListItem specialty || !AvailabilityDatePicker.SelectedDate.HasValue)
        {
            WriteResult("Selecione especialidade e data para buscar profissionais.");
            return;
        }

        if (!TryCreateClient(out var client))
        {
            return;
        }

        var date = DateOnly.FromDateTime(AvailabilityDatePicker.SelectedDate.Value);
        var selectedUnit = UnitComboBox.SelectedItem as UnitFilterOption;

        await ExecuteAsync(async cancellationToken =>
        {
            var result = await client.GetProfessionalsAsync(specialty.Id, date, selectedUnit?.Id, cancellationToken);
            if (!result.IsSuccess || result.Data is null)
            {
                WriteResult($"Erro ao buscar profissionais: {result.Error}");
                return;
            }

            ProfessionalsDataGrid.ItemsSource = result.Data;
            AvailabilityDataGrid.ItemsSource = null;
            SlotIdTextBox.Clear();
            HoldIdTextBox.Clear();

            WriteResult(result.Data.Count == 0
                ? "Nenhum profissional encontrado para os filtros selecionados."
                : $"{result.Data.Count} profissionais encontrados.");
        });
    }

    private async void ProfessionalsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfessionalsDataGrid.SelectedItem is not ProfessionalSearchItem professional)
        {
            return;
        }

        await ExecuteAsync(cancellationToken => LoadAvailabilityAsync(professional, cancellationToken));
    }

    private void AvailabilityDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AvailabilityDataGrid.SelectedItem is AvailableSlotItem slot)
        {
            SlotIdTextBox.Text = slot.SlotId.ToString();
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
            if (!result.IsSuccess || result.Data is null)
            {
                WriteResult($"Erro ao criar hold: {result.Error}");
                return;
            }

            HoldIdTextBox.Text = result.Data.HoldId.ToString();
            WriteResult($"Hold criado com sucesso.\nHoldId: {result.Data.HoldId}\nExpira em: {result.Data.ExpiresAt:yyyy-MM-dd HH:mm:ss}");

            if (ProfessionalsDataGrid.SelectedItem is ProfessionalSearchItem professional)
            {
                await LoadAvailabilityAsync(professional, cancellationToken);
            }
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
            if (!result.IsSuccess || result.Data is null)
            {
                WriteResult($"Erro ao confirmar hold: {result.Error}");
                return;
            }

            WriteResult($"Booking confirmado com sucesso.\nBookingId: {result.Data.BookingId}");

            if (ProfessionalsDataGrid.SelectedItem is ProfessionalSearchItem professional)
            {
                await LoadAvailabilityAsync(professional, cancellationToken);
            }
        });
    }

    private async Task LoadReferenceDataAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateClient(out var client))
        {
            return;
        }

        var specialtiesResult = await client.GetSpecialtiesAsync(cancellationToken);
        if (!specialtiesResult.IsSuccess || specialtiesResult.Data is null)
        {
            WriteResult($"Erro ao carregar especialidades: {specialtiesResult.Error}");
            return;
        }

        var unitsResult = await client.GetUnitsAsync(cancellationToken);
        if (!unitsResult.IsSuccess || unitsResult.Data is null)
        {
            WriteResult($"Erro ao carregar unidades: {unitsResult.Error}");
            return;
        }

        SpecialtyComboBox.ItemsSource = specialtiesResult.Data;
        if (specialtiesResult.Data.Count > 0)
        {
            SpecialtyComboBox.SelectedIndex = 0;
        }

        var unitOptions = new List<UnitFilterOption> { new(null, "Todas as unidades") };
        unitOptions.AddRange(unitsResult.Data.Select(unit => new UnitFilterOption(unit.Id, unit.Name)));
        UnitComboBox.ItemsSource = unitOptions;
        UnitComboBox.SelectedIndex = 0;

        WriteResult("Filtros clinicos carregados.");
    }

    private async Task LoadAvailabilityAsync(ProfessionalSearchItem professional, CancellationToken cancellationToken)
    {
        if (!AvailabilityDatePicker.SelectedDate.HasValue)
        {
            WriteResult("Selecione uma data para consultar disponibilidade.");
            return;
        }

        if (PeriodComboBox.SelectedItem is not AvailabilityPeriod period)
        {
            WriteResult("Selecione um periodo valido.");
            return;
        }

        if (!TryCreateClient(out var client))
        {
            return;
        }

        var date = DateOnly.FromDateTime(AvailabilityDatePicker.SelectedDate.Value);
        var result = await client.GetAvailabilityAsync(professional.ProfessionalId, date, period, professional.ClinicUnitId, cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            WriteResult($"Erro ao carregar disponibilidade: {result.Error}");
            return;
        }

        AvailabilityDataGrid.ItemsSource = result.Data;
        WriteResult(result.Data.Count == 0
            ? "Nao ha horarios disponiveis para este profissional e periodo."
            : $"{result.Data.Count} horarios disponiveis carregados.");
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
            WriteResult("A operacao excedeu o tempo limite de 15 segundos.");
        }
        catch (HttpRequestException ex)
        {
            WriteResult($"Erro de conexao com a API: {ex.Message}");
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
            WriteResult("URL base da API invalida.");
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
        {
            return true;
        }

        WriteResult($"Campo invalido: {fieldName} deve ser um GUID valido.");
        return false;
    }

    private bool TryReadRequired(string? value, string fieldName, out string parsed)
    {
        parsed = value?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(parsed))
        {
            return true;
        }

        WriteResult($"Campo obrigatorio: {fieldName}.");
        return false;
    }

    private void ToggleUi(bool enabled)
    {
        SearchProfessionalsButton.IsEnabled = enabled;
        CreateHoldButton.IsEnabled = enabled;
        ConfirmHoldButton.IsEnabled = enabled;
        SpecialtyComboBox.IsEnabled = enabled;
        UnitComboBox.IsEnabled = enabled;
        AvailabilityDatePicker.IsEnabled = enabled;
        PeriodComboBox.IsEnabled = enabled;
    }

    private void WriteResult(string message)
    {
        ResultTextBox.Text = message;
    }

    private sealed record UnitFilterOption(Guid? Id, string Name);
}
