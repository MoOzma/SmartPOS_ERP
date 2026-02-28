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

## 📸 System Screenshots

###  🛒 Home
![Home](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Home.png)
### 1️⃣ Authentication & User Management
| Login  | Admin Registration | Personnel Management |
| :---: | :---: | :---: |
| ![Login](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Login.png) | ![Add user](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Add%20user.png) | ![Personnel management](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Personnel%20management.png) |

### 2️⃣ Inventory & Supply Chain
| Suppliers Directory | Supply Invoice | Supply of Goods |
| :---: | :---: | :---: |
| ![Suppliers Directory](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Suppliers%20Directory.png) | ![Supply invoice](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Supply%20invoice.png) | ![Supply of goods](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Supply%20of%20goods.png) |

### 3️⃣ Sales & Transactions
| Sales Display Screen | Sales Invoice | Sales Invoices List |
| :---: | :---: | :---: |
| ![Sales display screen](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Sales%20display%20screen.png) | ![Sales invoice](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Sales%20invoice.png) | ![Sales invoices](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Sales%20invoices.png) |

### 4️⃣ Accounting & Financial Dashboards
| Financial Dashboard | Profit and Loss | Account Statement |
| :---: | :---: | :---: |
| ![Financial dashboard](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Financial%20dashboard.png) | ![Profit and Loss](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Profit%20and%20Loss.png) | ![account statement](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/account%20statement.png) |

### 5️⃣ Reports & Logs
| Warehouse Shortages | Movement Log | Data Modification |
| :---: | :---: | :---: |
| ![Warehouse Shortages Report](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Warehouse%20Shortages%20Report.png) | ![Movement log](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Movement%20log.png) | ![Data modification](https://github.com/MoOzma/SmartPOS_ERP/blob/master/ScreenShots/Data%20modification.png) |

## 📝 How to Run

1.  Clone the repository.
2.  Update the connection string in `appsettings.json`.
3.  Run `Update-Database` in the Package Manager Console.
4.  The default login is `admin` / `123`.

---
Developed as part of a deep-dive into C# and ASP.NET Core concepts.
