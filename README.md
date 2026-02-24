# SmartPOS & ERP System

A robust and scalable Point of Sale (POS) and Enterprise Resource Planning (ERP) system built with **ASP.NET Core MVC**. This project is designed to manage retail operations, including inventory tracking, sales, supplier accounts, and financial reporting.

## 🚀 Key Features

### 🔐 Advanced Security & Authentication
* **Custom Login Middleware:** Implemented a custom `LoginCheckMiddleware` to secure all internal routes and handle unauthorized access.
* **Secure Password Hashing:** Uses **BCrypt.Net** for high-security password hashing, ensuring user credentials are never stored in plain text.
* **Session Management:** State management using secure Cookies and Sessions to track user activity and roles.

### 📦 Inventory & Product Management
* **Real-time Stock Tracking:** Automated stock level updates upon sales and purchase transactions.
* **Barcode Integration:** Support for rapid product lookup and entry using barcode scanners.
* **Dynamic UI:** Interactive product grid with status indicators for low-stock items.

### 💰 Financial & Supplier Management
* **Supplier Accounts:** Comprehensive tracking of supplier invoices, payments, and remaining balances.
* **Expense Management:** Integrated module to record and categorize business expenses.
* **Profit & Loss Reporting:** Dynamic dashboards calculating monthly sales, costs, and net profit margins.

### 📊 Reporting & Analytics
* **Sales History:** Detailed logs of all transactions with filtering by date, month, or year.
* **Stock Movement:** In-depth reports showing the history of every item from entry to sale.

## 🛠️ Tech Stack

* **Framework:** ASP.NET Core 8.0 (MVC Pattern)
* **Database:** SQL Server
* **ORM:** Entity Framework Core (Code First)
* **Security:** BCrypt.Net, Custom Middleware, Identity (Base setup)
* **Frontend:** Bootstrap 5, CSS3, JavaScript, Razor Pages
* **Tools:** Visual Studio, LINQ

## 🏗️ Project Structure Highlights

* **Middleware:** `LoginCheckMiddleware.cs` acts as a security gatekeeper for the application.
* **Controllers:** Cleanly separated logic for `Products`, `Purchases`, `Expenses`, and `Account` management.
* **Database Seeding:** Automated creation of a default "Admin" user with hashed credentials upon first run.

## 📸 Screenshots

*(Tip: You can upload your screenshots to a folder named 'screenshots' in your repo and link them here)*
- **Point of Sale Interface:** Clean and responsive UI for quick checkout.
- **Supplier Ledger:** Real-time tracking of debts and payments.
- **Profit Dashboard:** Visual representation of business health.

## 📝 How to Run

1.  Clone the repository.
2.  Update the connection string in `appsettings.json`.
3.  Run `Update-Database` in the Package Manager Console.
4.  The default login is `admin` / `123`.

---
Developed as part of a deep-dive into C# and ASP.NET Core concepts.
