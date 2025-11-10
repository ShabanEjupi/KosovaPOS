# KosovaPOS - Point of Sale System

A modern WPF-based Point of Sale (POS) system built with .NET 8.

## Features

- **Cash Register**: Process sales transactions with barcode scanning
- **Inventory Management**: Track articles, stock levels, and suppliers
- **Business Partners**: Manage customers and suppliers
- **Purchases**: Track and manage purchase orders
- **Reports & Analytics**: Sales reports and business analytics
- **Fiscal Printer Integration**: Support for fiscal printers
- **Receipt Printing**: Print sales receipts
- **User Management**: Multi-user support with audit logging

## Requirements

- .NET 8.0 SDK or Runtime
- Windows OS (WPF application)
- Visual Studio 2022 or later (for development)

## Building the Project

### Using Visual Studio
1. Open `KosovaPOS.sln` in Visual Studio
2. Build the solution (Ctrl+Shift+B)
3. Run the application (F5)

### Using Command Line
```bash
dotnet build
dotnet run
```

## Project Structure

- **Models/**: Data models (Article, Purchase, Receipt, etc.)
- **Windows/**: WPF windows/views
- **Services/**: Business logic services
- **Database/**: Entity Framework DbContext
- **Migrations/**: Database migrations
- **Resources/**: UI resources (themes, translations)
- **Helpers/**: Utility classes

## Database

The application uses SQLite for local data storage with Entity Framework Core.

## Git Repository Setup

This repository has been initialized with git. To push to GitHub:

1. Create a new **private** repository on GitHub (https://github.com/new)
2. Name it: `KosovaPOS` or your preferred name
3. Make sure to select **Private** repository
4. Don't initialize with README, .gitignore, or license
5. Run these commands:

```bash
git remote add origin https://github.com/YOUR-USERNAME/YOUR-REPO-NAME.git
git branch -M main
git push -u origin main
```

## License

Private/Proprietary
