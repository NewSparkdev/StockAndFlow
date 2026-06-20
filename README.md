# Stock & Flow

**Professional inventory management and business tracking system for retail businesses.**

Stock & Flow is a Windows desktop application designed to help small to medium-sized retail businesses manage their inventory, track sales, monitor expenses, and gain real-time insights into their financial performance.

![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![WPF](https://img.shields.io/badge/WPF-Windows-blue)
![C#](https://img.shields.io/badge/C%23-Latest-green)
![License](https://img.shields.io/badge/license-MIT-blue)

## Features

### 📦 Inventory Management
- Add, edit, and track inventory items with SKU support
- Track cost per unit, sale price, and quantity on hand
- Upload product images
- Categorize products for easy organization
- Low stock alerts and notifications
- Real-time inventory value calculations

### 💰 Sales Tracking
- Record sales with automatic inventory adjustment
- Support for tax calculations (multiple tax rates)
- Customer information tracking
- Sales history and analytics
- Multi-item transactions via shopping cart
- Invoice generation (PDF)

### 📊 Expense Management
- Track business expenses by category
- Link expenses to specific inventory items
- Receipt image uploads
- Date range filtering
- Expense analytics and reporting

### 📈 Real-Time Analytics Dashboard
- Total revenue and expenses
- Net profit calculations
- Profit margins (gross and net)
- Current inventory value
- Automatic metric updates

### 🔄 Shopify Integration
- Two-way product synchronization
- Automatic order import
- Inventory level updates
- Configurable sync intervals

### 📄 Export & Reporting
- Export data to Excel (XLSX)
- Generate invoices as PDF
- Detailed sales and expense reports
- Date range filtering

## Tech Stack

- **Language**: C# 10
- **Framework**: .NET 10.0
- **UI**: WPF (Windows Presentation Foundation)
- **Architecture**: MVVM (Model-View-ViewModel)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Data Storage**: JSON-based local storage
- **PDF Generation**: SkiaSharp (`SKDocument`) — cross-platform (Windows/Android/iOS)
- **Excel Export**: ClosedXML
- **API Integration**: Shopify Admin API

## Architecture

Stock & Flow follows a clean, event-driven MVVM architecture:

```
┌─────────────────┐
│     Views       │  WPF XAML Views
└────────┬────────┘
         │ Data Binding
┌────────▼────────┐
│   ViewModels    │  INotifyPropertyChanged, Commands
└────────┬────────┘
         │ Service Calls
┌────────▼────────┐
│    Services     │  Business Logic Layer
└────────┬────────┘
         │ CRUD Operations
┌────────▼────────┐
│     Models      │  Data Models (POCOs)
└─────────────────┘
```

### Key Architectural Features:
- **Dependency Injection**: Services and ViewModels are registered and injected
- **Event-Driven**: Services raise events on data changes
- **Automatic Calculations**: Financial metrics recalculate automatically
- **Separation of Concerns**: Clear layer separation for maintainability
- **Testable**: Service layer can be easily unit tested

## Getting Started

### Prerequisites

- Windows 10/11 (64-bit)
- .NET 10.0 SDK (for development)

### Installation

#### For End Users:
Download the installer from the [Releases](https://github.com/yourusername/StockAndFlow/releases) page and run the setup wizard.

#### For Developers:

1. Clone the repository:
```bash
git clone https://github.com/yourusername/StockAndFlow.git
cd StockAndFlow
```

2. Open the solution:
```bash
# Open in Visual Studio 2022
start StockAndFlow.sln

# Or use .NET CLI
cd StockAndFlow
dotnet restore
dotnet build
```

3. Run the application:
```bash
dotnet run --project StockAndFlow/StockAndFlow.csproj
```

## Configuration

### Data Storage
By default, data is stored in JSON files in the `Data/` folder:
- `inventoryitems.json` - Product inventory
- `sales.json` - Sales transactions
- `expenses.json` - Business expenses
- `businesssettings.json` - Application settings

### Shopify Integration (Optional)
To enable Shopify integration:
1. Open **Settings** → **Business Settings**
2. Enable "Shopify Integration"
3. Enter your Shopify store name
4. Provide API credentials (API key and access token)
5. Configure sync settings

## Project Structure

```
StockAndFlow/
├── Models/              # Data models (InventoryItem, Sale, Expense, etc.)
├── Services/            # Business logic services
│   ├── DataService.cs           # JSON data persistence
│   ├── InventoryService.cs      # Inventory management
│   ├── SalesService.cs          # Sales processing
│   ├── ExpenseService.cs        # Expense tracking
│   ├── CalculationService.cs    # Financial calculations
│   └── ShopifyService.cs        # Shopify API integration
├── ViewModels/          # MVVM ViewModels
├── Views/               # WPF Views (XAML)
│   └── Dialogs/        # Modal dialogs
├── Commands/            # ICommand implementations
└── App.xaml.cs         # Application startup & DI configuration
```

## Development

### Building from Source

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Publish self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## Screenshots

*Screenshots coming soon*

## Roadmap

- [ ] Database backend option (SQLite/SQL Server)
- [ ] Multi-location inventory support
- [ ] Barcode scanning integration
- [ ] Advanced reporting with charts
- [ ] Multi-user support with access controls
- [ ] Cloud backup and sync
- [ ] Mobile companion app

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [WPF](https://github.com/dotnet/wpf)
- PDF generation by [SkiaSharp](https://github.com/mono/SkiaSharp)
- Excel export by [ClosedXML](https://github.com/ClosedXML/ClosedXML)
- Icons from [Material Design Icons](https://materialdesignicons.com/)

## Support

For issues, questions, or suggestions, please open an issue on GitHub or contact [your-email@example.com](mailto:your-email@example.com).

---

**Stock & Flow** - Simplifying retail business management, one transaction at a time.
