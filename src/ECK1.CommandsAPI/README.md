## Setup Instructions

1. Clone the repository:
   ```
   git clone <repository-url>
   cd Commands
   ```

2. Restore dependencies:
   ```
   dotnet restore
   ```

3. Update the connection string in `appsettings.json` to point to your SQL database.

4. Run the application:
   ```
   dotnet run
   ```

5. Access the API at `http://localhost:5000` (or the port specified in your launch settings).

- OR -

4. Run Deploy/RunLocally.ps1 to run in local k8s (if present)

## Usage

- The API exposes endpoints defined in `SampleController.cs`.
- Use tools like Postman or curl to interact with the API.
- Refer to the controller methods for available endpoints and their expected request/response formats.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request for any enhancements or bug fixes.