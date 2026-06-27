# PicoERP — سیستم مدیریت جامع کافه نت

[![Build Status](https://github.com/yourorg/picoerp/workflows/PicoERP%20CI%2FCD/badge.svg)](https://github.com/yourorg/picoerp/actions)

## معرفی

**پیکو ERP** یک سیستم مدیریت تجاری کامل برای کافه نت است که با فناوری‌های مدرن و استانداردهای تجاری ایران ساخته شده.

### ویژگی‌های اصلی

- ✅ کاملاً فارسی و راست‌به‌چپ (RTL)
- ✅ تقویم شمسی (جلالی)
- ✅ اعداد فارسی / انگلیسی (قابل تنظیم)
- ✅ مدیریت درآمد با دسته‌بندی نامحدود
- ✅ مدیریت هزینه (کاری، شخصی، کارمند)
- ✅ مدیریت حساب‌های مالی (صندوق، بانک، کارتخوان)
- ✅ مدیریت کامل کارمندان
- ✅ حضور و غیاب
- ✅ حقوق و دستمزد + فیش حقوقی PDF
- ✅ گزارش‌های مالی (PDF + Excel)
- ✅ داشبورد اجرایی با نمودار
- ✅ پشتیبان‌گیری و بازیابی SQLite
- ✅ حالت تاریک / روشن
- ✅ JWT Authentication

## استک فناوری

| لایه | فناوری |
|------|--------|
| Backend | ASP.NET Core 9 Blazor Server |
| UI | MudBlazor 8 |
| Database | SQLite + EF Core 9 |
| PDF | QuestPDF |
| Excel | ClosedXML |
| Auth | JWT |
| Logging | Serilog |
| Architecture | Clean Architecture + DI |

## راه‌اندازی سریع

### پیش‌نیازها
- .NET SDK 9.0
- (اختیاری) Docker

### اجرا بدون Docker

```bash
cd src/PicoERP.Web
dotnet run
```

مرورگر را روی `http://localhost:5000` باز کنید.

**اطلاعات ورود پیش‌فرض:**
- نام کاربری: `admin`
- رمز عبور: `Admin@123`

### اجرا با Docker

```bash
docker-compose up -d
```

## ساختار پروژه

```
PicoERP/
├── src/
│   ├── PicoERP.Domain/          # موجودیت‌ها، Enum ها، ابزار فارسی
│   ├── PicoERP.Application/     # DTOها، Interface ها، Common
│   ├── PicoERP.Infrastructure/  # EF Core، Services، PDF، Excel
│   └── PicoERP.Web/             # Blazor Server، MudBlazor، صفحات
├── Dockerfile
├── docker-compose.yml
└── PicoERP.sln
```

## ماژول‌ها

| ماژول | مسیر |
|-------|------|
| داشبورد | `/` |
| درآمدها | `/incomes` |
| هزینه‌ها | `/expenses` |
| حساب‌های مالی | `/accounts` |
| کارمندان | `/employees` |
| حضور و غیاب | `/attendance` |
| حقوق | `/salaries` |
| گزارش‌ها | `/reports/income` |
| پشتیبان‌گیری | `/backup` |
| تنظیمات | `/settings` |

## مجوز

تمامی حقوق محفوظ است © ۱۴۰۵
