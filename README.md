# 🍳 PantryChef AI Vision API

This repository contains the.NET 8 Azure Function microservice for the PantryChef application. This service functions as the "brain" for the AI-Powered Pantry feature, acting as a secure and efficient intermediary between the PantryChef Android app and the Azure AI Vision service.

![Visual_Studio-2022-purple](<https://img.shields.io/badge/Visual_Studio-2022-purple>)
![.NET-8-blueviolet](<https://img.shields.io/badge/.NET-8-blueviolet>)

## 📖 Table of Contents

- [✨ Overview](#-overview)

- [🎯 Purpose](#-purpose)

- [🛠️ Tech Stack](#️-tech-stack)

- [🚀 Getting Started](#-getting-started)

  - [📋 Prerequisites](#-getting-started)

  - [💻 Running Locally](#-running-locally)

  - [☁️ Deploying to Azure](#️-deploying-to-azure)

- [🔌 API Usage](#-api-usage)

  - [Endpoint](#endpoint)

  - [Authentication](#authentication)

  - [Request Body](#request-body)

  - [Success Response (200 OK)](#success-response-200-ok)

  - [Error Response (400/500)](#error-response-400500)

## ✨ Overview

This project is a single-purpose, serverless API built using the.NET 8 Isolated Worker model for Azure Functions. It is designed to receive an image of a grocery item from a client (like the PantryChef Android app), process it, and return structured data about that item.

It is a core component of the "AI-Powered Pantry" feature, as outlined in the project's technical specifications.

## 🎯 Purpose

This API serves two critical functions, acting as an intelligent and secure "middle-layer":

1. **🔒 Security & Abstraction:** The primary goal is to **protect the Azure AI Vision API keys**. The secret keys are stored securely on the server in the Function App's configuration. The client application never has access to them, preventing malicious users from decompiling the app and stealing the keys.

2. **⚙️ Data Transformation:** The raw JSON response from the Azure AI Vision service is massive and complex. This function performs "heavy-lifting" on the server by parsing this complex response and transforming it into a simple, clean, and specific JSON object (`PantryAiResponse`) that the client application requires. This saves mobile data, battery, and processing power on the client device.

The data flow is as follows:

1. The Android Client sends an image as `multipart/form-data` to this Azure Function.

2. This Function receives the image stream and forwards it to the Azure AI Vision service.

3. Azure AI Vision analyzes the image and returns a complex JSON with `Tags`, `Objects`, and `Captions`.

4. This Function intelligently maps those results to the simple `PantryAiData` model (e.g., finding the best `itemName` and `category`).

5. This Function returns the simple JSON to the Android client, which is then used to pre-populate the "Add Pantry Item" form.

## 🛠️ Tech Stack

- **.NET 8 Isolated Worker:** The modern, high-performance runtime for.NET on Azure Functions.

- **Azure Functions:** Serverless, event-driven compute.

- **Azure AI Vision SDK:** The official `Azure.AI.Vision.ImageAnalysis` client library used to communicate with the AI service.

- **HttpMultipartParser:** A necessary NuGet package to parse `multipart/form-data` from the `HttpRequestData` object in the .NET Isolated model.

## 🚀 Getting Started

To run this project, you will need to set up both the required Azure resources and your local environment.

### 📋 Prerequisites

**Before you can run this project, you must have:**

- An **Azure Account** with an active subscription.

- **Visual Studio 2026** with the "Azure development" workload installed.

- **.NET 8 SDK**.

- An **Azure AI Vision Resource** created in the Azure Portal.

**To create the AI Vision Resource:**

1. Log in to the **Azure Portal**.

2. Search for and create an **"Azure Computer Vision"** resource.

3. **Region:** When creating it, you **must** select a region that supports the captioning feature, such as `East US`.

4. **Pricing Tier:** Select the `F0` (Free) tier to get started.

5. Once deployed, navigate to the resource and go to the **"Keys and Endpoint"** blade.

6. You will need two values: **`KEY 1`** and the **`Endpoint`**. Copy these to a safe place.

**Example of Implementation:**

![Images/Computer_Vision.png](Images/Computer_Vision.png)

### 💻 Running Locally

1. **Clone the Repository:**

    ``` Bash
    git clone https://github.com/PROG7314-POE-SSB/PROG7314-POE-Part-3-AI-Vision.git

    ```

2. **Open in Visual Studio:** Open the `PantryChef.slnx` file in Visual Studio 2022.

3. **Restore Packages:** Right-click the project in the Solution Explorer and select "Manage NuGet Packages...". Ensure all packages (like `Azure.AI.Vision.ImageAnalysis` and `HttpMultipartParser`) are restored.

4. **Create `local.settings.json`:**

    - In the Solution Explorer, right-click the `PantryChef.VisionApi` project.

    - Select **Add** > **New Item...**.

    - Search for "JSON" and select **JavaScript JSON Configuration File**.

    - Name the file exactly: `local.settings.json`.

    - Paste the following code into the file:

    ``` JSON
    {
      "IsEncrypted": false,
      "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "VISION_ENDPOINT": "PASTE_YOUR_ENDPOINT_HERE",
        "VISION_KEY": "PASTE_YOUR_KEY_HERE"
      },
      "Host": {
        "CORS": "*"
      }
    }

    ```

    - Replace `PASTE_YOUR_ENDPOINT_HERE` and `PASTE_YOUR_KEY_HERE` with the values you copied from the Azure portal. The `CORS: "*"` setting is for allowing local testing from any origin.^10^

    - **IMPORTANT:** Right-click `local.settings.json` in the Solution Explorer, select **Properties**, and set **"Copy to Output Directory"** to **"Copy if newer"**.

5. **Run the Project:** Press **F5** or the "Start Debugging" button. A console window will appear, and it will show you the local URL for your function, which is typically `http://localhost:7064/api/ProcessPantryImage`.

6. **Test:** You can now test this local endpoint using Postman.

### ☁️ Deploying to Azure

To deploy this function to a public Azure endpoint, follow these steps:

1. **Create the Function App:**

    - In the Azure Portal, create a new **"Function App"** resource.

    - **Runtime Stack:** `.NET`

    - **Version:** `8 Isolated`

    - **Operating System:** `Windows` (or `Linux`)

    - **Hosting Plan:** `Consumption (Serverless)`

2. **Publish from Visual Studio:**

    - In Visual Studio, right-click the project in Solution Explorer and select **"Publish..."**.

    - **Target:** `Azure`.

    - **Specific Target:** `Azure Function App`.

    - Select the Function App you created in Step 1 and follow the wizard to deploy the code.

3. **⛔ CRITICAL: Configure Deployed Settings:**

    By default, your deployed app will fail with a 500 Internal Server Error because it does not have the VISION_ENDPOINT or VISION_KEY. The local.settings.json file is not deployed to Azure.

    - Go to your new Function App in the **Azure Portal**.

    - In the left-hand menu, go to **Settings** > **Environment variables**.

    - Under **"Application settings"**, click **"+ Add application setting"**.

    - Add the following two settings:

        - **Name:** `VISION_ENDPOINT`

        - **Value:** `(Your AI Vision Endpoint URL)`

        - **Name:** `VISION_KEY`

        - **Value:** `(Your AI Vision Secret Key)`

    - Click **"Save"**. This will restart your Function App with the correct credentials.

4. **Configure CORS:**

    - To allow your Android app or a web client to call the function, you must configure CORS.

    - In the Function App menu, go to **API** > **CORS**.

    - Add the URLs of your client applications. For testing purposes, you can add `*`, but for production, you should restrict this to your specific client domains.

**Example of Implementation:**

![Images/Function.png](Images/Function.png)

## 🔌 API Usage

### Endpoint

`POST https://<YOUR-FUNCTION-APP-NAME>.azurewebsites.net/api/ProcessPantryImage`

### Authentication

This function is secured with `Function` level authorization. You must provide the function's API key as a query parameter.

1. In the Azure Portal, navigate to your Function App -> **Functions** -> `ProcessPantryImage`.

2. Click on **"Get Function Url"**.

3. Copy the full URL, which will include the `?code=...` key.

**Example:** `https://cv-pantrychef.azurewebsites.net/api/ProcessPantryImage?code=ycDc...wGgRA==`

### Request Body

The request must be sent with a `Content-Type` of `multipart/form-data`. It requires one part:

- **Key:** `image`

- **Type:** `File`

- **Value:** (The image file you want to analyze)

### Success Response (200 OK)

A successful request will return the following JSON structure:

``` JSON
{
  "success": true,
  "data": {
    "itemName": "A logo with a rainbow stripe",
    "description": "a city with tall buildings",
    "category": "Other",
    "estimatedExpiry": null,
    "nutritionalInfo": {},
    "confidence": 0.7481964826583862
  },
  "error": null
}

```

### Error Response (400/500)

If an error occurs (e.g., no file is uploaded, or the AI service fails), the response will be:

``` JSON
{
  "success": false,
  "data": null,
  "error": "An error message describing the problem."
}

```
