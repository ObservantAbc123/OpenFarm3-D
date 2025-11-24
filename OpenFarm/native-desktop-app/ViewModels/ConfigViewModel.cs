using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatabaseAccess;
using DatabaseAccess.Models;
using RabbitMQHelper;
using Color = Avalonia.Media.Color;

namespace native_desktop_app.ViewModels;

// Purpose
//   ConfigViewModel for the Config tab. Allows operators to configure system settings,
//   starting with Material Pricing.
// -----------------------------------------------------------------------------
public class ConfigViewModel : ViewModelBase
{
    /// <summary>
    ///     Currently selected material row in the UI (for "Change price" / "View history" actions).
    /// </summary>
    private MaterialPricingRow? _selectedMaterialPricingRow;

    private PrinterConfigRow? _selectedPrinter;
    private EmailRuleViewModel? _selectedOutOfOfficeRule;
    private EmailRuleViewModel? _selectedTimeBasedRule;


    // -----------------------------
    // Constructor
    // -----------------------------
    public ConfigViewModel(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper)
        : base(databaseAccessHelper, rmqHelper)
    {
        // MATERIAL PRICING COMMANDS
        RefreshMaterialPricingCommand =
            new RelayCommand(async () => await LoadMaterialPricingAsync());

        ShowChangeMaterialPriceDialogCommand =
            new RelayCommand(async () => await ShowChangeMaterialPriceDialogAsync(SelectedMaterialPricingRow));

        ShowMaterialPriceHistoryDialogCommand =
            new RelayCommand(async () => await ShowMaterialPriceHistoryDialogAsync(SelectedMaterialPricingRow));

        AddMaterialCommand =
            new RelayCommand(async () => await ShowAddMaterialDialogAsync());

        DeleteMaterialCommand =
            new RelayCommand(async () => await DeleteSelectedMaterialAsync(SelectedMaterialPricingRow));

        // PRINTER COMMANDS
        RefreshPrintersCommand =
            new RelayCommand(async () => await LoadPrintersAsync());

        AddPrinterCommand =
            new RelayCommand(async () => await AddPrinterAsync());

        DeletePrinterCommand =
            new RelayCommand(async () => await DeleteSelectedPrinterAsync());

        SavePrinterCommand =
            new RelayCommand(async () => await SaveSelectedPrinterAsync());

        AddPrinterModelCommand =
            new RelayCommand(async () => await AddPrinterModelAsync());
        
        AddOutOfOfficeRuleCommand = new RelayCommand(AddOutOfOfficeRule);
        DeleteOutOfOfficeRuleCommand = new RelayCommand(DeleteOutOfOfficeRule);
        AddTimeRuleCommand = new RelayCommand(AddTimeRule);
        DeleteTimeRuleCommand = new RelayCommand(DeleteTimeRule);
        SaveAllCommand = new RelayCommand(async () => await SaveAllAsync());

        // INITIAL LOADS
        _ = LoadMaterialPricingAsync();
        _ = LoadPrinterModelsAsync();
        _ = LoadPrintersAsync();
        _ = LoadEmailRulesAsync();
    }

    // ------------------ Commands ------------------
    public ICommand RefreshMaterialPricingCommand { get; }
    public ICommand ShowChangeMaterialPriceDialogCommand { get; }
    public ICommand ShowMaterialPriceHistoryDialogCommand { get; }
    public ICommand AddMaterialCommand { get; }
    public ICommand DeleteMaterialCommand { get; }
    public ICommand AddPrinterModelCommand { get; }
    public ICommand AddOutOfOfficeRuleCommand { get; }
    public ICommand DeleteOutOfOfficeRuleCommand { get; }
    public ICommand AddTimeRuleCommand { get; }
    public ICommand DeleteTimeRuleCommand { get; }
    public ICommand SaveAllCommand { get; }

    
    // ========================================================================
//  EMAIL AUTO-REPLY RULES
// ========================================================================

    private readonly ObservableCollection<EmailRuleViewModel> _outOfOfficeRules = new();
    private readonly ObservableCollection<EmailRuleViewModel> _timeBasedRules = new();
    private readonly List<int> _deletedRuleIds = new();

    public ObservableCollection<EmailRuleViewModel> OutOfOfficeRules => _outOfOfficeRules;
    public ObservableCollection<EmailRuleViewModel> TimeBasedRules => _timeBasedRules;

    public EmailRuleViewModel? SelectedOutOfOfficeRule
    {
        get => _selectedOutOfOfficeRule;
        set
        {
            if (_selectedOutOfOfficeRule == value)
                return;

            _selectedOutOfOfficeRule = value;
            OnPropertyChanged();
        }
    }

    public EmailRuleViewModel? SelectedTimeBasedRule
    {
        get => _selectedTimeBasedRule;
        set
        {
            if (_selectedTimeBasedRule == value)
                return;

            _selectedTimeBasedRule = value;
            OnPropertyChanged();
        }
    }
    
    


    // ========================================================================
    //  PRINTER CONFIGURATION
    // ========================================================================

    public ObservableCollection<PrinterConfigRow> Printers { get; } = new();
    public ObservableCollection<PrinterModelConfigRow> PrinterModels { get; } = new();

    public PrinterConfigRow? SelectedPrinter
    {
        get => _selectedPrinter;
        set
        {
            if (_selectedPrinter == value)
                return;

            _selectedPrinter = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshPrintersCommand { get; }
    public ICommand AddPrinterCommand { get; }
    public ICommand DeletePrinterCommand { get; }
    public ICommand SavePrinterCommand { get; }


    // ========================================================================
    //  MATERIAL PRICING
    // ========================================================================

    /// <summary>
    ///     Collection of materials and their current active price period, for binding.
    /// </summary>
    public ObservableCollection<MaterialPricingRow> MaterialPricing { get; } = new();

    public MaterialPricingRow? SelectedMaterialPricingRow
    {
        get => _selectedMaterialPricingRow;
        set
        {
            if (_selectedMaterialPricingRow == value)
                return;

            _selectedMaterialPricingRow = value;
            OnPropertyChanged(); // notifies that SelectedMaterialPricingRow changed
        }
    }

    public async Task LoadPrintersAsync()
    {
        Printers.Clear();
        var printers = await _databaseAccessHelper.Printers.GetPrintersAsync();

        foreach (var printer in printers)
        {
            var modelName =
                await _databaseAccessHelper.PrinterModels.GetPrinterModelNameAsync(printer.PrinterModelId) ?? "Unknown";
            Printers.Add(new PrinterConfigRow
            {
                Id = printer.Id,
                Name = printer.Name,
                IpAddress = printer.IpAddress,
                ApiKey = printer.ApiKey,
                ModelName = modelName,
                Enabled = printer.Enabled,
                CurrentlyPrinting = printer.CurrentlyPrinting,
                Autostart = printer.Autostart ?? false
            });
        }

        if (Printers.Count > 0 && SelectedPrinter is null)
            SelectedPrinter = Printers[0];
    }

    public async Task LoadPrinterModelsAsync()
    {
        PrinterModels.Clear();
        var models = await _databaseAccessHelper.PrinterModels.GetPrinterModelsAsync();

        foreach (var model in models)
            PrinterModels.Add(new PrinterModelConfigRow
            {
                Id = model.Id,
                Model = model.Model,
                Autostart = model.Autostart
            });
    }

    private async Task AddPrinterAsync()
    {
        // Make sure we have fresh models & materials
        await LoadPrinterModelsAsync();
        await LoadMaterialPricingAsync();

        if (PrinterModels.Count == 0)
        {
            await ShowSimpleDialogAsync(
                "No printer models",
                "No printer models were found. Please create at least one printer model before adding a printer."
            );
            return;
        }

        // Show full "Add printer" dialog
        var result = await ShowAddPrinterDialogAsync();
        if (result is null)
            return; // user cancelled

        // Create the printer itself
        var createResult = await _databaseAccessHelper.Printers.CreatePrinterWithConfigAsync(
            result.Name,
            result.PrinterModelId,
            result.IpAddress,
            result.ApiKey,
            result.Enabled,
            result.Autostart
        );

        if (createResult != TransactionResult.Succeeded)
        {
            await ShowSimpleDialogAsync("Error", "Failed to create the printer. Please try again.");
            return;
        }

        // TODO: hook up loaded materials after you know the new printer's ID.
        // If CreatePrinterWithConfigAsync returns the new printer or its ID, use it here.
        // Example pattern (adjust to your actual helper API):
        //
        // var newPrinterId = ...; // get from helper or re-query by name
        // foreach (var materialId in result.LoadedMaterialIds)
        // {
        //     await _databaseAccessHelper.LoadedMaterials.LoadMaterialAsync(newPrinterId, materialId);
        // }

        await LoadPrintersAsync();
    }

    private async Task<NewPrinterDialogResult?> ShowAddPrinterDialogAsync()
    {
        var dialog = new Window
        {
            Title = "Add Printer",
            Width = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        root.Children.Add(new TextBlock
        {
            Text = "Create a new printer",
            FontSize = 16,
            FontWeight = FontWeight.Bold
        });

        // --- Model selection ---
        var modelPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        modelPanel.Children.Add(new TextBlock
        {
            Text = "Model:",
            Width = 90,
            VerticalAlignment = VerticalAlignment.Center
        });

        var modelCombo = new ComboBox
        {
            Width = 220,
            ItemsSource = PrinterModels,
            SelectedItem = PrinterModels.FirstOrDefault()
        };
        modelPanel.Children.Add(modelCombo);
        root.Children.Add(modelPanel);

        // --- Name ---
        var namePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        namePanel.Children.Add(new TextBlock
        {
            Text = "Name:",
            Width = 90,
            VerticalAlignment = VerticalAlignment.Center
        });

        var nameTextBox = new TextBox
        {
            Width = 220,
            Text = "New Printer"
        };
        namePanel.Children.Add(nameTextBox);
        root.Children.Add(namePanel);

        // --- IP / Location ---
        var ipPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        ipPanel.Children.Add(new TextBlock
        {
            Text = "IP / Location:",
            Width = 90,
            VerticalAlignment = VerticalAlignment.Center
        });

        var ipTextBox = new TextBox
        {
            Width = 220,
            Watermark = "e.g. 10.0.0.5 or 'Protospace A1'"
        };
        ipPanel.Children.Add(ipTextBox);
        root.Children.Add(ipPanel);

        // --- API Key ---
        var apiPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        apiPanel.Children.Add(new TextBlock
        {
            Text = "API Key:",
            Width = 90,
            VerticalAlignment = VerticalAlignment.Center
        });

        var apiTextBox = new TextBox
        {
            Width = 220,
            Watermark = "(optional)"
        };
        apiPanel.Children.Add(apiTextBox);
        root.Children.Add(apiPanel);

        // --- Enabled + Autostart ---
        var flagsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Margin = new Thickness(0, 4, 0, 4)
        };

        var enabledCheck = new CheckBox
        {
            Content = "Enabled",
            IsChecked = true
        };
        var autostartCheck = new CheckBox
        {
            Content = "Autostart",
            IsChecked = PrinterModels.FirstOrDefault() is { Autostart: true }
        };

        flagsPanel.Children.Add(enabledCheck);
        flagsPanel.Children.Add(autostartCheck);
        root.Children.Add(flagsPanel);

        // --- Loaded materials selection ---
        root.Children.Add(new TextBlock
        {
            Text = "Loaded materials (optional):",
            Margin = new Thickness(0, 8, 0, 4)
        });

        var materialsList = new ListBox
        {
            ItemsSource = MaterialPricing,
            SelectionMode = SelectionMode.Multiple,
            Height = 140
        };

// Show each material as "PLA - Black", etc.
        materialsList.ItemTemplate = new FuncDataTemplate<MaterialPricingRow>((row, _) =>
            new TextBlock
            {
                Text = row?.Name ?? string.Empty,
                Margin = new Thickness(4, 2)
            });

        root.Children.Add(materialsList);


        // --- Buttons ---
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        NewPrinterDialogResult? result = null;

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };
        cancelButton.Click += (_, _) =>
        {
            result = null;
            dialog.Close();
        };

        var createButton = new Button
        {
            Content = "Create",
            Width = 80
        };
        createButton.Click += (_, _) =>
        {
            var chosenModel = modelCombo.SelectedItem as PrinterModelConfigRow;
            var name = nameTextBox.Text?.Trim();

            if (chosenModel is null || string.IsNullOrWhiteSpace(name))
            {
                // basic validation: focus the offending control
                if (chosenModel is null)
                {
                    modelCombo.Focus();
                }
                else
                {
                    nameTextBox.Focus();
                    nameTextBox.SelectAll();
                }

                return;
            }

            var selectedMaterials = materialsList.SelectedItems?
                .OfType<MaterialPricingRow>()
                .ToList() ?? new List<MaterialPricingRow>();

            result = new NewPrinterDialogResult
            {
                Name = name,
                PrinterModelId = chosenModel.Id,
                IpAddress = string.IsNullOrWhiteSpace(ipTextBox.Text) ? null : ipTextBox.Text!.Trim(),
                ApiKey = string.IsNullOrWhiteSpace(apiTextBox.Text) ? null : apiTextBox.Text!.Trim(),
                Enabled = enabledCheck.IsChecked ?? true,
                Autostart = autostartCheck.IsChecked ?? false,
                LoadedMaterialIds = selectedMaterials.Select(m => m.MaterialId).ToList()
            };

            dialog.Close();
        };

        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(createButton);
        root.Children.Add(buttonsPanel);

        dialog.Content = root;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await dialog.ShowDialog(parentWindow);

        return result;
    }

    private async Task LoadEmailRulesAsync()
    {
        OutOfOfficeRules.Clear();
        TimeBasedRules.Clear();

        // Uses DatabaseAccessHelper.EmailAutoReplyRules helper
        var rules = await _databaseAccessHelper.EmailAutoReplyRules.GetAllAsync();

        foreach (var rule in rules)
        {
            var vm = EmailRuleViewModel.FromEntity(rule);

            // ruletype is an int in the entity, so cast to the enum
            var ruleType = (Emailautoreplyrule.EmailRuleType)rule.Ruletype;

            if (ruleType == Emailautoreplyrule.EmailRuleType.OutOfOffice)
                OutOfOfficeRules.Add(vm);
            else if (ruleType == Emailautoreplyrule.EmailRuleType.TimeWindow)
                TimeBasedRules.Add(vm);
        }

        if (SelectedOutOfOfficeRule is null)
            SelectedOutOfOfficeRule = OutOfOfficeRules.FirstOrDefault();

        if (SelectedTimeBasedRule is null)
            SelectedTimeBasedRule = TimeBasedRules.FirstOrDefault();
    }


    private void AddOutOfOfficeRule()
    {
        var vm = new EmailRuleViewModel
        {
            Label = "New out-of-office rule",
            RuleType = Emailautoreplyrule.EmailRuleType.OutOfOffice,
            IsEnabled = true,
            Priority = 100,
            StartDate = DateTimeOffset.Now,
            EndDate = DateTimeOffset.Now
        };

        OutOfOfficeRules.Add(vm);
        SelectedOutOfOfficeRule = vm;
    }

private void DeleteOutOfOfficeRule()
{
    if (SelectedOutOfOfficeRule is null)
        return;

    if (SelectedOutOfOfficeRule.Id > 0)
        _deletedRuleIds.Add(SelectedOutOfOfficeRule.Id);

    OutOfOfficeRules.Remove(SelectedOutOfOfficeRule);
    SelectedOutOfOfficeRule = OutOfOfficeRules.FirstOrDefault();
}

private void AddTimeRule()
{
    var vm = new EmailRuleViewModel
    {
        Label = "New time-based rule",
        RuleType = Emailautoreplyrule.EmailRuleType.TimeWindow,
        IsEnabled = true,
        Priority = 100,
        DaysOfWeek = Emailautoreplyrule.DayOfWeekFlags.Weekdays,
        StartTime = new TimeSpan(18, 0, 0),   // 6 PM default
        EndTime = new TimeSpan(23, 59, 0)     // 11:59 PM default
    };

    TimeBasedRules.Add(vm);
    SelectedTimeBasedRule = vm;
}

private void DeleteTimeRule()
{
    if (SelectedTimeBasedRule is null)
        return;

    if (SelectedTimeBasedRule.Id > 0)
        _deletedRuleIds.Add(SelectedTimeBasedRule.Id);

    TimeBasedRules.Remove(SelectedTimeBasedRule);
    SelectedTimeBasedRule = TimeBasedRules.FirstOrDefault();
}

private async Task SaveAllAsync()
{
    // Save out-of-office rules
    foreach (var vm in OutOfOfficeRules)
    {
        var entity = vm.ToEntity();
        await _databaseAccessHelper.EmailAutoReplyRules.UpdateAsync(entity);
        vm.Id = entity.Emailautoreplyruleid; // <-- updated
    }

    // Save time-based rules
    foreach (var vm in TimeBasedRules)
    {
        var entity = vm.ToEntity();
        await _databaseAccessHelper.EmailAutoReplyRules.UpdateAsync(entity);
        vm.Id = entity.Emailautoreplyruleid; // <-- updated
    }

    // Delete removed rules
    foreach (var id in _deletedRuleIds)
    {
        await _databaseAccessHelper.EmailAutoReplyRules.DeleteAsync(id);
    }
    _deletedRuleIds.Clear();

    await ShowSimpleDialogAsync("Saved", "Email auto-reply rules have been saved.");
}



    private async Task<PrinterModelConfigRow?> ShowSelectPrinterModelDialogAsync()
    {
        var dialog = new Window
        {
            Title = "Select printer model",
            Width = 360,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        root.Children.Add(new TextBlock
        {
            Text = "Choose a printer model for the new printer:",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        });

        var modelCombo = new ComboBox
        {
            Width = 180,
            ItemsSource = PrinterModels,
            SelectedItem = PrinterModels.FirstOrDefault()
        };

        root.Children.Add(modelCombo);

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        PrinterModelConfigRow? selectedModel = null;

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };
        cancelButton.Click += (_, _) =>
        {
            selectedModel = null;
            dialog.Close();
        };

        var createButton = new Button
        {
            Content = "Create",
            Width = 80
        };
        createButton.Click += (_, _) =>
        {
            selectedModel = modelCombo.SelectedItem as PrinterModelConfigRow;
            dialog.Close();
        };

        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(createButton);

        root.Children.Add(buttonsPanel);
        dialog.Content = root;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await dialog.ShowDialog(parentWindow);

        return selectedModel;
    }


    private async Task SaveSelectedPrinterAsync()
    {
        if (SelectedPrinter == null) return;
        await _databaseAccessHelper.Printers.UpdatePrinterNameAsync(SelectedPrinter.Id, SelectedPrinter.Name);
        await _databaseAccessHelper.Printers.UpdatePrinterIpAddressAsync(SelectedPrinter.Id, SelectedPrinter.IpAddress);
        await _databaseAccessHelper.Printers.UpdatePrinterApiKeyAsync(SelectedPrinter.Id, SelectedPrinter.ApiKey);
        await _databaseAccessHelper.Printers.SetPrinterEnabledStatusAsync(SelectedPrinter.Id, SelectedPrinter.Enabled);
        await _databaseAccessHelper.Printers.SetPrinterAutostartStatusAsync(SelectedPrinter.Id,
            SelectedPrinter.Autostart);

        await ShowSimpleDialogAsync("Saved", $"Printer “{SelectedPrinter.Name}” updated successfully.");
    }

    private async Task DeleteSelectedPrinterAsync()
    {
        if (SelectedPrinter == null) return;

        var (result, dependentPrints, dependentLoadedMaterials) =
            await _databaseAccessHelper.Printers.DeletePrinterRestrictedAsync(SelectedPrinter.Id);

        switch (result)
        {
            case TransactionResult.Succeeded:
                Printers.Remove(SelectedPrinter);
                break;
            case TransactionResult.Abandoned:
                await ShowSimpleDialogAsync("Cannot Delete",
                    $"Printer “{SelectedPrinter.Name}” has {dependentPrints.Count} active print(s) or " +
                    $"{dependentLoadedMaterials.Count} loaded material(s). Remove these before deletion.");
                break;
            default:
                await ShowSimpleDialogAsync("Error", "Failed to delete printer.");
                break;
        }
    }

    private async Task AddPrinterModelAsync()
    {
        var dialog = new Window
        {
            Title = "Add Printer Model",
            Width = 360,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        root.Children.Add(new TextBlock
        {
            Text = "Create a new printer model",
            FontSize = 16,
            FontWeight = FontWeight.Bold
        });

        var modelPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        modelPanel.Children.Add(new TextBlock
        {
            Text = "Model:",
            VerticalAlignment = VerticalAlignment.Center
        });

        var modelTextBox = new TextBox
        {
            Watermark = "e.g. Bambu A1, Prusa MK3"
        };

        modelPanel.Children.Add(modelTextBox);
        root.Children.Add(modelPanel);

        var autostartCheckBox = new CheckBox
        {
            Content = "Autostart by default",
            IsChecked = false
        };
        root.Children.Add(autostartCheckBox);

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        string? modelName = null;
        var autostart = false;

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };
        cancelButton.Click += (_, _) =>
        {
            modelName = null;
            dialog.Close();
        };

        var saveButton = new Button
        {
            Content = "Save",
            Width = 80
        };
        saveButton.Click += (_, _) =>
        {
            var text = modelTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                // Simple validation feedback: focus/select
                modelTextBox.Focus();
                modelTextBox.SelectAll();
                return;
            }

            modelName = text;
            autostart = autostartCheckBox.IsChecked ?? false;
            dialog.Close();
        };

        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(saveButton);

        root.Children.Add(buttonsPanel);
        dialog.Content = root;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await dialog.ShowDialog(parentWindow);

        // If user cancelled or left empty, bail
        if (string.IsNullOrWhiteSpace(modelName))
            return;

        // TODO: adjust this helper name/signature if your actual API differs
        var result = await _databaseAccessHelper.PrinterModels
            .CreatePrinterModelAsync(modelName, autostart);

        if (result == TransactionResult.Succeeded)
        {
            await LoadPrinterModelsAsync();
            await ShowSimpleDialogAsync("Model created",
                $"Printer model \"{modelName}\" was created successfully.");
        }
        else
        {
            await ShowSimpleDialogAsync("Error",
                "Failed to create printer model. Please try again.");
        }
    }

    /// <summary>
    ///     Loads or reloads the material pricing table.
    ///     Call this when the Config tab/pricing tab opens, or after changes.
    /// </summary>
    public async Task LoadMaterialPricingAsync()
    {
        MaterialPricing.Clear();

        var materials = await _databaseAccessHelper.Materials.GetMaterialsAsync();

        foreach (var material in materials)
        {
            var activePricePeriod =
                await _databaseAccessHelper.MaterialPricePeriods.GetMaterialPricePeriodAsync(material.Id);

            MaterialPricing.Add(
                new MaterialPricingRow(_databaseAccessHelper, _rmqHelper, material, activePricePeriod));
        }
    }

    /// <summary>
    ///     Changes the price for a given material by creating a new MaterialPricePeriod
    ///     and ending the old one (if present).
    /// </summary>
    public async Task<TransactionResult> ChangeMaterialPriceAsync(MaterialPricingRow row, decimal newPrice)
    {
        if (row == null) return TransactionResult.NoAction;

        var result = await _databaseAccessHelper.MaterialPricePeriods
            .UpdateMaterialPriceAsync(row.Material.Id, newPrice);

        if (result == TransactionResult.Succeeded) await LoadMaterialPricingAsync();

        return result;
    }

    /// <summary>
    ///     Gets the full price history (all MaterialPricePeriods) for a given row's material.
    /// </summary>
    public Task<List<MaterialPricePeriod>> GetMaterialPriceHistoryAsync(MaterialPricingRow row)
    {
        if (row == null) return Task.FromResult(new List<MaterialPricePeriod>());

        return _databaseAccessHelper.MaterialPricePeriods.GetMaterialPricePeriodsAsync(row.Material.Id);
    }

    private async Task DeleteSelectedMaterialAsync(MaterialPricingRow? row)
    {
        if (row is null)
            return;

        // 1) Ask the user to confirm
        var confirmed = await ConfirmDeleteMaterialAsync(row);
        if (!confirmed)
            return;

        // 2) Try to delete using your restricted helper
        var (result, dependentPrintJobs, dependentLoadedMaterials) =
            await _databaseAccessHelper.Materials.DeleteMaterialRestrictedAsync(row.MaterialId);

        switch (result)
        {
            case TransactionResult.Succeeded:
                // Remove from the ObservableCollection so the UI updates
                MaterialPricing.Remove(row);
                break;

            case TransactionResult.Abandoned:
                // There are dependent entities; show why we can't delete
                await ShowDeleteBlockedDialogAsync(row, dependentPrintJobs.Count, dependentLoadedMaterials.Count);
                break;

            case TransactionResult.NotFound:
                // It was already deleted elsewhere; refresh the list
                await ShowSimpleDialogAsync(
                    "Material not found",
                    "This material no longer exists in the database. The list will be refreshed.");
                await LoadMaterialPricingAsync();
                break;

            case TransactionResult.Failed:
                await ShowSimpleDialogAsync(
                    "Error",
                    "Failed to delete the material. Please try again.");
                break;
        }
    }

    private async Task<bool> ConfirmDeleteMaterialAsync(MaterialPricingRow row)
    {
        var dialog = new Window
        {
            Title = "Delete Material",
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        root.Children.Add(new TextBlock
        {
            Text = $"Are you sure you want to delete “{row.Name}”?",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        });

        root.Children.Add(new TextBlock
        {
            Text = "This cannot be undone.",
            Foreground = new SolidColorBrush(Color.Parse("#fb4934")),
            FontSize = 12
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        bool? confirmed = null;

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };
        cancelButton.Click += (_, _) =>
        {
            confirmed = false;
            dialog.Close();
        };

        var deleteButton = new Button
        {
            Content = "Delete",
            Width = 80,
            Background = new SolidColorBrush(Color.Parse("#cc241d")),
            Foreground = new SolidColorBrush(Colors.White)
        };
        deleteButton.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };

        buttons.Children.Add(cancelButton);
        buttons.Children.Add(deleteButton);

        root.Children.Add(buttons);
        dialog.Content = root;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await dialog.ShowDialog(parentWindow);

        return confirmed == true;
    }

    private async Task ShowSimpleDialogAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        root.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        });

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        okButton.Click += (_, _) => dialog.Close();

        root.Children.Add(okButton);
        dialog.Content = root;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await dialog.ShowDialog(parentWindow);
    }

    private Task ShowDeleteBlockedDialogAsync(
        MaterialPricingRow row,
        int printJobCount,
        int loadedMaterialsCount)
    {
        var message =
            $"The material “{row.Name}” cannot be deleted because it is still in use.\n\n" +
            $"- {printJobCount} print job(s) reference this material.\n" +
            $"- {loadedMaterialsCount} printer(s) currently have this material loaded.\n\n" +
            "Remove these dependencies first, then try deleting the material again.";

        return ShowSimpleDialogAsync("Cannot delete material", message);
    }


    // ========================================================================
    //  Helpers
    // ========================================================================
    private async Task AddOrUpdateMaterialAsync(string typeText, string colorText, bool inStock)
    {
        if (string.IsNullOrWhiteSpace(typeText) || string.IsNullOrWhiteSpace(colorText))
            return;

        var typeName = typeText.Trim();
        var colorName = colorText.Trim();

        // 1. Ensure material type exists (PLA, ABS, etc.)
        var typeId = await _databaseAccessHelper.MaterialTypes
            .GetMaterialTypeIdAsync(typeName);

        if (typeId is null || typeId <= 0)
        {
            var typeResult = await _databaseAccessHelper.MaterialTypes
                .CreateMaterialTypeAsync(typeName);

            if (typeResult == TransactionResult.Failed)
                return; // TODO: show error message

            typeId = await _databaseAccessHelper.MaterialTypes
                .GetMaterialTypeIdAsync(typeName);
            if (typeId is null || typeId <= 0)
                return;
        }

        // 2. Ensure color exists (Black, White, etc.)
        // ColorHelper already normalizes to lowercase internally.
        var colorId = await _databaseAccessHelper.Colors
            .GetColorIdAsync(colorName);

        if (colorId is null || colorId <= 0)
        {
            var colorResult = await _databaseAccessHelper.Colors
                .CreateColorAsync(colorName);

            if (colorResult == TransactionResult.Failed)
                return; // TODO: show error message

            colorId = await _databaseAccessHelper.Colors
                .GetColorIdAsync(colorName);
            if (colorId is null || colorId <= 0)
                return;
        }

        // 3. Create the material (or no-op if it already exists)
        TransactionResult materialResult;
        if (inStock)
            materialResult = await _databaseAccessHelper.Materials
                .CreateMaterialInStockAsync(typeId.Value, colorId.Value);
        else
            materialResult = await _databaseAccessHelper.Materials
                .CreateMaterialNotInStockAsync(typeId.Value, colorId.Value);

        // materialResult:
        // - Succeeded → new material was created
        // - NoAction  → that type+color already existed
        // - Failed    → something went wrong

        if (materialResult == TransactionResult.Failed)
            return; // TODO: show error

        // 4. Refresh the pricing grid so it appears in the list
        await LoadMaterialPricingAsync();
    }


    // ========================================================================
    //  DIALOGS (like in PrintJobsViewModel)
    // ========================================================================

    private async Task ShowAddMaterialDialogAsync()
    {
        // Build initial type/color lists from currently known materials
        var materialTypes = MaterialPricing
            .Select(m => m.MaterialTypeName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != "Unknown type")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        var colors = MaterialPricing
            .Select(m => m.ColorName)
            .Where(n => !string.IsNullOrWhiteSpace(n) && n != "Unknown color")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        var window = new Window
        {
            Title = "Add Material",
            Width = 420,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        var title = new TextBlock
        {
            Text = "Create a new material",
            FontSize = 16,
            FontWeight = FontWeight.Bold
        };

        // --- Type row: ComboBox + "Add Type" button ---
        var typePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        typePanel.Children.Add(new TextBlock
        {
            Text = "Type:",
            Width = 70,
            VerticalAlignment = VerticalAlignment.Center
        });

        var typeComboBox = new ComboBox
        {
            Width = 180,
            ItemsSource = materialTypes,
            SelectedItem = materialTypes.FirstOrDefault()
        };

        var addTypeButton = new Button
        {
            Content = "Add Type",
            Width = 90
        };
        addTypeButton.Click += async (_, _) =>
        {
            var newTypeName = await ShowAddMaterialTypeDialogAsync();
            if (string.IsNullOrWhiteSpace(newTypeName))
                return;

            if (!materialTypes.Contains(newTypeName, StringComparer.OrdinalIgnoreCase))
            {
                materialTypes.Add(newTypeName);
                materialTypes.Sort(StringComparer.OrdinalIgnoreCase);
                typeComboBox.ItemsSource = null;
                typeComboBox.ItemsSource = materialTypes;
            }

            typeComboBox.SelectedItem = materialTypes
                .FirstOrDefault(n => string.Equals(n, newTypeName, StringComparison.OrdinalIgnoreCase));
        };

        typePanel.Children.Add(typeComboBox);
        typePanel.Children.Add(addTypeButton);

        // --- Color row: ComboBox + "Add Color" button ---
        var colorPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        colorPanel.Children.Add(new TextBlock
        {
            Text = "Color:",
            Width = 70,
            VerticalAlignment = VerticalAlignment.Center
        });

        var colorComboBox = new ComboBox
        {
            Width = 180,
            ItemsSource = colors,
            SelectedItem = colors.FirstOrDefault()
        };

        var addColorButton = new Button
        {
            Content = "Add Color",
            Width = 90
        };
        addColorButton.Click += async (_, _) =>
        {
            var newColorName = await ShowAddColorDialogAsync();
            if (string.IsNullOrWhiteSpace(newColorName))
                return;

            if (!colors.Contains(newColorName, StringComparer.OrdinalIgnoreCase))
            {
                colors.Add(newColorName);
                colors.Sort(StringComparer.OrdinalIgnoreCase);
                colorComboBox.ItemsSource = null;
                colorComboBox.ItemsSource = colors;
            }

            colorComboBox.SelectedItem = colors
                .FirstOrDefault(n => string.Equals(n, newColorName, StringComparison.OrdinalIgnoreCase));
        };

        colorPanel.Children.Add(colorComboBox);
        colorPanel.Children.Add(addColorButton);

        // --- In stock checkbox ---
        var inStockCheckBox = new CheckBox
        {
            Content = "In stock",
            IsChecked = true
        };

        // --- Buttons ---
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };
        cancelButton.Click += (_, _) => window.Close();

        var saveButton = new Button
        {
            Content = "Save",
            Width = 80
        };
        saveButton.Click += async (_, _) =>
        {
            var selectedTypeName = typeComboBox.SelectedItem as string;
            var selectedColorName = colorComboBox.SelectedItem as string;
            var inStock = inStockCheckBox.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(selectedTypeName) ||
                string.IsNullOrWhiteSpace(selectedColorName))
            {
                await ShowSimpleDialogAsync(
                    "Missing information",
                    "Please choose both a material type and a color (or use \"Add Type\" / \"Add Color\").");
                return;
            }

            // Reuse your existing helper – it will ensure the type/color exist in DB
            await AddOrUpdateMaterialAsync(selectedTypeName, selectedColorName, inStock);
            window.Close();
        };

        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(saveButton);

        // Assemble dialog
        root.Children.Add(title);
        root.Children.Add(typePanel);
        root.Children.Add(colorPanel);
        root.Children.Add(inStockCheckBox);
        root.Children.Add(buttonsPanel);

        window.Content = root;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await window.ShowDialog(parentWindow);
    }

    private async Task<string?> ShowAddMaterialTypeDialogAsync()
    {
        var dialog = new Window
        {
            Title = "Add Material Type",
            Width = 360,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        root.Children.Add(new TextBlock
        {
            Text = "Enter a new material type (e.g. PLA, ABS):",
            TextWrapping = TextWrapping.Wrap
        });

        var textBox = new TextBox
        {
            Watermark = "e.g. PLA"
        };
        root.Children.Add(textBox);

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        string? createdTypeName = null;

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };
        cancelButton.Click += (_, _) =>
        {
            createdTypeName = null;
            dialog.Close();
        };

        var saveButton = new Button
        {
            Content = "Save",
            Width = 80
        };
        saveButton.Click += async (_, _) =>
        {
            var typeName = textBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(typeName))
            {
                textBox.Focus();
                textBox.SelectAll();
                return;
            }

            var result = await _databaseAccessHelper.MaterialTypes.CreateMaterialTypeAsync(typeName);
            if (result == TransactionResult.Failed)
            {
                await ShowSimpleDialogAsync("Error", "Failed to create material type. Please try again.");
                return;
            }

            createdTypeName = typeName;
            dialog.Close();
        };

        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(saveButton);
        root.Children.Add(buttonsPanel);

        dialog.Content = root;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await dialog.ShowDialog(parentWindow);

        return createdTypeName;
    }

    private async Task<string?> ShowAddColorDialogAsync()
    {
        var dialog = new Window
        {
            Title = "Add Color",
            Width = 360,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        root.Children.Add(new TextBlock
        {
            Text = "Enter a new color name (e.g. Black, Dark Green):",
            TextWrapping = TextWrapping.Wrap
        });

        var textBox = new TextBox
        {
            Watermark = "e.g. Black"
        };
        root.Children.Add(textBox);

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        string? createdColorName = null;

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };
        cancelButton.Click += (_, _) =>
        {
            createdColorName = null;
            dialog.Close();
        };

        var saveButton = new Button
        {
            Content = "Save",
            Width = 80
        };
        saveButton.Click += async (_, _) =>
        {
            var colorName = textBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(colorName))
            {
                textBox.Focus();
                textBox.SelectAll();
                return;
            }

            var result = await _databaseAccessHelper.Colors.CreateColorAsync(colorName);
            if (result == TransactionResult.Failed)
            {
                await ShowSimpleDialogAsync("Error", "Failed to create color. Please try again.");
                return;
            }

            createdColorName = colorName;
            dialog.Close();
        };

        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(saveButton);
        root.Children.Add(buttonsPanel);

        dialog.Content = root;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await dialog.ShowDialog(parentWindow);

        return createdColorName;
    }


    private async Task ShowChangeMaterialPriceDialogAsync(MaterialPricingRow? row)
    {
        if (row is null) return;

        var dialog = new Window
        {
            Title = $"Change Price - {row.Name}",
            Width = 360,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var root = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        var title = new TextBlock
        {
            Text = row.Name,
            FontSize = 16,
            FontWeight = FontWeight.Bold
        };

        var currentPricePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        currentPricePanel.Children.Add(new TextBlock { Text = "Current price:" });
        currentPricePanel.Children.Add(new TextBlock
        {
            Text = row.CurrentPrice?.ToString("0.##") ?? "Not set"
        });

        var newPricePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        newPricePanel.Children.Add(new TextBlock { Text = "New price:" });

        var priceTextBox = new TextBox
        {
            Width = 120,
            Text = row.CurrentPrice?.ToString("0.##") ?? string.Empty
        };
        newPricePanel.Children.Add(priceTextBox);

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };
        var saveButton = new Button
        {
            Content = "Save",
            Width = 80
        };

        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(saveButton);

        root.Children.Add(title);
        root.Children.Add(currentPricePanel);
        root.Children.Add(newPricePanel);
        root.Children.Add(buttonsPanel);

        dialog.Content = root;

        decimal? newPrice = null;

        cancelButton.Click += (_, __) =>
        {
            newPrice = null;
            dialog.Close();
        };

        saveButton.Click += (_, __) =>
        {
            if (decimal.TryParse(priceTextBox.Text, out var parsed))
            {
                newPrice = parsed;
                dialog.Close();
            }
            else
            {
                // basic feedback: select all and focus if invalid
                priceTextBox.Focus();
                priceTextBox.SelectAll();
            }
        };

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await dialog.ShowDialog(parentWindow);

        if (newPrice.HasValue) await ChangeMaterialPriceAsync(row, newPrice.Value);
    }

    private async Task ShowMaterialPriceHistoryDialogAsync(MaterialPricingRow? row)
    {
        if (row is null) return;

        var history = await GetMaterialPriceHistoryAsync(row);

        var window = new Window
        {
            Title = $"Price History - {row.Name}",
            Width = 480,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var border = new Border
        {
            Padding = new Thickness(16),
            Background = new SolidColorBrush(Color.Parse("#32302f")),
            CornerRadius = new CornerRadius(8)
        };

        var root = new StackPanel
        {
            Spacing = 10
        };

        var header = new TextBlock
        {
            Text = $"Price history for {row.Name}",
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = new SolidColorBrush(Color.Parse("#fbf1c7"))
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var listStack = new StackPanel
        {
            Spacing = 4
        };

        if (history.Count == 0)
            listStack.Children.Add(new TextBlock
            {
                Text = "No price history found.",
                Foreground = new SolidColorBrush(Color.Parse("#ebdbb2"))
            });
        else
            foreach (var period in history)
            {
                var line = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };

                var startText = period.CreatedAt.ToString("g");
                var endText = period.EndedAt.HasValue
                    ? period.EndedAt.Value.ToString("g")
                    : "Active";

                line.Children.Add(new TextBlock
                {
                    Text = startText,
                    Width = 140,
                    Foreground = new SolidColorBrush(Color.Parse("#d5c4a1"))
                });

                line.Children.Add(new TextBlock
                {
                    Text = "→",
                    Width = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.Parse("#d5c4a1"))
                });

                line.Children.Add(new TextBlock
                {
                    Text = endText,
                    Width = 140,
                    Foreground = new SolidColorBrush(Color.Parse("#d5c4a1"))
                });

                line.Children.Add(new TextBlock
                {
                    Text = period.Price.ToString("0.##"),
                    Foreground = new SolidColorBrush(Color.Parse("#b8bb26"))
                });

                listStack.Children.Add(line);
            }

        scroll.Content = listStack;

        var closeButton = new Button
        {
            Content = "Close",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        closeButton.Click += (_, __) => window.Close();

        root.Children.Add(header);
        root.Children.Add(scroll);
        root.Children.Add(closeButton);

        border.Child = root;
        window.Content = border;

        Window? parentWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            parentWindow = desktop.MainWindow;

        await window.ShowDialog(parentWindow);
    }
}

public sealed class MaterialPricingRow : ViewModelBase
{
    public MaterialPricingRow(
        DatabaseAccessHelper databaseAccessHelper,
        IRmqHelper rmqHelper,
        Material material,
        MaterialPricePeriod? activePricePeriod)
        : base(databaseAccessHelper, rmqHelper)
    {
        Material = material ?? throw new ArgumentNullException(nameof(material));
        ActivePricePeriod = activePricePeriod;
    }

    public Material Material { get; }

    public MaterialPricePeriod? ActivePricePeriod { get; private set; }

    public int MaterialId => Material.Id;

    // Just the category, e.g. "PLA", "ABS"
    public string MaterialTypeName => Material.MaterialType?.Type ?? "Unknown type";

    // Nice label for UI: "PLA - Black"
    public string Name => $"{MaterialTypeName} - {ColorName}";

    private string RawColorName => Material.MaterialColor?.Name ?? string.Empty;

    // UI-friendly color name (e.g., "black" -> "Black", "dark green" -> "Dark Green")
    public string ColorName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RawColorName))
                return "Unknown color";

            var lower = RawColorName.ToLowerInvariant();
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(lower);
        }
    }

    public bool IsInStock
    {
        get => Material.InStock;
        set
        {
            if (value == Material.InStock)
                return;

            Material.InStock = value;

            // notify the UI
            OnPropertyChanged();

            // fire-and-forget DB update
            _ = UpdateStockAsync(value);
        }
    }


    public decimal? CurrentPrice => ActivePricePeriod?.Price;
    public DateTime? PriceLastUpdated => ActivePricePeriod?.CreatedAt;

    // UI-friendly strings for the columns
    public string CurrentPriceDisplay =>
        CurrentPrice.HasValue ? CurrentPrice.Value.ToString("0.##") : "Not set";

    public string PriceLastUpdatedDisplay =>
        PriceLastUpdated.HasValue
            ? PriceLastUpdated.Value.ToLocalTime().ToString("g")
            : "Never";

    public override string ToString()
    {
        return Name;
    }

    private async Task UpdateStockAsync(bool inStock)
    {
        if (inStock)
            await _databaseAccessHelper.Materials.SetMaterialInStockAsync(MaterialId);
        else
            await _databaseAccessHelper.Materials.SetMaterialOutOfStockAsync(MaterialId);
    }

    public void UpdateActivePricePeriod(MaterialPricePeriod? newPeriod)
    {
        ActivePricePeriod = newPeriod;

        // If you wire up INotifyPropertyChanged in ViewModelBase, you can raise:
        // OnPropertyChanged(nameof(CurrentPrice));
        // OnPropertyChanged(nameof(PriceLastUpdated));
        // OnPropertyChanged(nameof(CurrentPriceDisplay));
        // OnPropertyChanged(nameof(PriceLastUpdatedDisplay));
    }
}

public sealed class PrinterConfigRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? ApiKey { get; set; }
    public string ModelName { get; set; } = "Unknown";
    public bool Enabled { get; set; }
    public bool CurrentlyPrinting { get; set; }
    public bool Autostart { get; set; }

    public string StatusText =>
        !Enabled ? "Disabled" :
        CurrentlyPrinting ? "Printing" :
        "Idle";
}

public sealed class PrinterModelConfigRow
{
    public int Id { get; set; }
    public string Model { get; set; } = string.Empty;
    public bool Autostart { get; set; }

    public override string ToString()
    {
        return Model;
    }
}

public sealed class NewPrinterDialogResult
{
    public string Name { get; init; } = string.Empty;
    public int PrinterModelId { get; init; }
    public string? IpAddress { get; init; }
    public string? ApiKey { get; init; }
    public bool Enabled { get; init; }
    public bool Autostart { get; init; }
    public List<int> LoadedMaterialIds { get; init; } = new();
}

public class EmailRuleViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _id;
    private string _label = string.Empty;
    private Emailautoreplyrule.EmailRuleType _ruleType;
    private DateTimeOffset? _startDate;
    private DateTimeOffset? _endDate;
    private TimeSpan? _startTime;
    private TimeSpan? _endTime;
    private Emailautoreplyrule.DayOfWeekFlags _daysOfWeek;
    private string _subject = string.Empty;
    private string _body = string.Empty;
    private bool _isEnabled = true;
    private int _priority = 100;

    public int Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(nameof(Id)); }
    }

    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(nameof(Label)); }
    }

    public Emailautoreplyrule.EmailRuleType RuleType
    {
        get => _ruleType;
        set { _ruleType = value; OnPropertyChanged(nameof(RuleType)); }
    }

    // For Avalonia DatePicker (DateTimeOffset?)
    public DateTimeOffset? StartDate
    {
        get => _startDate;
        set { _startDate = value; OnPropertyChanged(nameof(StartDate)); }
    }

    public DateTimeOffset? EndDate
    {
        get => _endDate;
        set { _endDate = value; OnPropertyChanged(nameof(EndDate)); }
    }

    // For Avalonia TimePicker (TimeSpan)
    public TimeSpan? StartTime
    {
        get => _startTime;
        set { _startTime = value; OnPropertyChanged(nameof(StartTime)); }
    }

    public TimeSpan? EndTime
    {
        get => _endTime;
        set { _endTime = value; OnPropertyChanged(nameof(EndTime)); }
    }

    public Emailautoreplyrule.DayOfWeekFlags DaysOfWeek
    {
        get => _daysOfWeek;
        set { _daysOfWeek = value; OnPropertyChanged(nameof(DaysOfWeek)); }
    }

    public string Subject
    {
        get => _subject;
        set { _subject = value; OnPropertyChanged(nameof(Subject)); }
    }

    public string Body
    {
        get => _body;
        set { _body = value; OnPropertyChanged(nameof(Body)); }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
    }

    public int Priority
    {
        get => _priority;
        set { _priority = value; OnPropertyChanged(nameof(Priority)); }
    }

    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // -------- Mapping from DB entity -> ViewModel --------
    public static EmailRuleViewModel FromEntity(Emailautoreplyrule entity) => new()
    {
        Id       = entity.Emailautoreplyruleid,
        Label    = entity.Label,
        RuleType = (Emailautoreplyrule.EmailRuleType)entity.Ruletype,

        StartDate = entity.Startdate.HasValue
            ? new DateTimeOffset(
                  entity.Startdate.Value.Year,
                  entity.Startdate.Value.Month,
                  entity.Startdate.Value.Day,
                  0, 0, 0,
                  TimeSpan.Zero)
            : (DateTimeOffset?)null,

        EndDate = entity.Enddate.HasValue
            ? new DateTimeOffset(
                  entity.Enddate.Value.Year,
                  entity.Enddate.Value.Month,
                  entity.Enddate.Value.Day,
                  0, 0, 0,
                  TimeSpan.Zero)
            : (DateTimeOffset?)null,

        StartTime  = entity.Starttime?.ToTimeSpan(),
        EndTime    = entity.Endtime?.ToTimeSpan(),
        DaysOfWeek = (Emailautoreplyrule.DayOfWeekFlags)entity.Daysofweek,
        Subject    = entity.Subject,
        Body       = entity.Body,
        IsEnabled  = entity.Isenabled,
        Priority   = entity.Priority
    };

    // -------- Mapping ViewModel -> DB entity --------
    public Emailautoreplyrule ToEntity()
    {
        var entity = new Emailautoreplyrule
        {
            Emailautoreplyruleid = Id,
            Label       = Label,
            Ruletype    = (int)RuleType,

            Startdate = StartDate.HasValue
                ? DateOnly.FromDateTime(StartDate.Value.DateTime)
                : null,

            Enddate = EndDate.HasValue
                ? DateOnly.FromDateTime(EndDate.Value.DateTime)
                : null,

            Starttime = StartTime.HasValue
                ? TimeOnly.FromTimeSpan(StartTime.Value)
                : null,

            Endtime = EndTime.HasValue
                ? TimeOnly.FromTimeSpan(EndTime.Value)
                : null,

            Daysofweek = (int)DaysOfWeek,
            Subject    = Subject,
            Body       = Body,
            Isenabled  = IsEnabled,
            Priority   = Priority
            // Createdatutc / Updatedatutc will be set in the helper / service layer
        };

        return entity;
    }
}