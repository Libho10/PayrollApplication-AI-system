using System;
using Oracle.ManagedDataAccess.Client;
using System.Text;
using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data; 

namespace PayrollApplication
{
    class Program
    {
        static List<string> GetJobIDs(OracleConnection conn)
        {
            List<string> jobIds = new List<string>();
            string sql = "SELECT jobID FROM Jobs";

            try
            {
                using (OracleCommand cmd = new OracleCommand(sql, conn))
                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        jobIds.Add(reader.GetString(0));
                }
            } catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Job IDs: {ex.Message}");
            }
            return jobIds;
        }

        static List<int> GetDepartmentIDs(OracleConnection conn)
        {
            List<int> deptIds = new List<int>();
            string sql = "SELECT DepartmentID FROM Departments";
            try
            {
                using (OracleCommand cmd = new OracleCommand(sql, conn))
                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        deptIds.Add(reader.GetInt32(0));
                }
            } catch (Exception ex)
            {
                Console.WriteLine($"error fetching Department IDs: {ex.Message}");
            }
            return deptIds;
        }
        static List<PayrollData> GetPayrollDataFromOracle(OracleConnection conn)
        {
            var payrollList = new List<PayrollData>();
            string sql = @"SELECT EmployeeID, Salary, CommissionPct, DepartmentID, JobID, ManagerID FROM Employees";

            using (var cmd = new OracleCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    payrollList.Add(new PayrollData
                    {
                        EmployeeID = reader.IsDBNull(0) ? 0 : Convert.ToSingle(reader.GetDecimal(0)),
                        Salary = reader.IsDBNull(1) ? 0 : Convert.ToSingle(reader.GetDecimal(1)),
                        CommissionPct = reader.IsDBNull(2) ? 0 : Convert.ToSingle(reader.GetDecimal(2)),
                        DepartmentID = reader.IsDBNull(3) ? 0 : Convert.ToSingle(reader.GetDecimal(3)),
                        JobID = reader.IsDBNull(4) ? string.Empty: reader.GetString(4), //<--- edited
                        ManagerID = reader.IsDBNull(5) ? 0 : Convert.ToSingle(reader.GetDecimal(5))
                    });
                }

            }
            return payrollList;

        }
        static void Main(string[] args)
        {
            string connectionString = "User Id=SYSTEM;Password=mypassword;Data Source=localhost:1521/xepdb1;Pooling=false";

            using (OracleConnection conn = new OracleConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    Console.WriteLine("Connected to Oracle Database Successfully.");

                    var payrollData = GetPayrollDataFromOracle(conn);
                    Console.WriteLine($"Fetched {payrollData.Count} payroll records."); ; 
                    var mlContext = new MLContext(); // creating ML context 
                    var dataView = mlContext.Data.LoadFromEnumerable(payrollData);

                    Console.WriteLine("Loaded payroll CSV data for anomaly detection.");

                    var pipeline = mlContext.Transforms.Categorical.OneHotEncoding( outputColumnName: "JobIDEncoded", inputColumnName: nameof(PayrollData.JobID))
                        .Append(mlContext.Transforms.Concatenate("Features", 
                        nameof(PayrollData.EmployeeID),
                        nameof(PayrollData.Salary),
                        nameof(PayrollData.CommissionPct),                    
                        nameof(PayrollData.DepartmentID),
                          "JobIDEncoded",
                        nameof(PayrollData.ManagerID))).Append(mlContext.AnomalyDetection.Trainers.RandomizedPca(featureColumnName: "Features", rank: 3));

                    var model = pipeline.Fit(dataView); // Trains the anomaly detection model. 
                    Console.WriteLine("Trained anomaly detection model");
                    var transformatedData = model.Transform(dataView); //will use the model to make prediction s on the same dataset. 
                    var predictions = mlContext.Data.CreateEnumerable<PayrollAnomalyPrediction>(transformatedData, reuseRowObject: false);

                    Console.WriteLine("\nEmployeeID | AnomalyScore");
                    foreach (var pred in predictions)
                    {
                        Console.WriteLine($"{pred.EmployeeID} | {pred.AnomalyScore:0.0000}");
                    }

                    bool exit = false;
                    while (!exit)
                    {
                        Console.WriteLine("\n____Payroll Management System____");
                        Console.WriteLine("1. View all Employees");
                        Console.WriteLine("2. Search Employee ID");
                        Console.WriteLine("3. Add New Employee");
                        Console.WriteLine("4. Update Employee");
                        Console.WriteLine("5. Delete Employee");
                        Console.WriteLine("6. Exit");
                        Console.Write("Select options (1-6): ");
                        string choice = Console.ReadLine();

                        Console.Clear();

                        switch (choice)
                        {
                            case "1":
                                Console.WriteLine("___Employee List___");
                                ViewAllEmployees(conn);
                                break;
                            case "2":
                                Console.WriteLine("___Search Employee___");
                                SearchEmployeeID(conn);
                                break;
                            case "3":
                                Console.WriteLine("___Add New Employee___");
                                AddEmployee(conn);
                                break;
                            case "4":
                                Console.WriteLine("___Update Employee___");
                                UpdateEmployee(conn);
                                break;
                            case "5":
                                Console.WriteLine("___Delete Employee___");
                                DeleteEmployee(conn);
                                break;
                            case "6":
                                exit = true;
                                Console.WriteLine("Existing Application...");
                                break;
                            default:
                                Console.WriteLine("invalid option. Select option (1-6)");
                                break;

                        }
                        if (!exit)
                        {
                            Console.Write("\nPress any key to return to Main Menu");
                            Console.ReadKey();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error connecting to database:" + ex.Message);
                }
            }

          

            Console.WriteLine("Press any key to exit system");
            Console.ReadKey();

           
           
        }
        static void ViewAllEmployees(OracleConnection conn)
        {
           try { 
                string sql = "SELECT EmployeeID, FirstName, LastName FROM Employees";

                 using (OracleCommand cmd = new OracleCommand(sql, conn))
                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    Console.WriteLine("\nEmployee List:");
                    while (reader.Read())
                     {
                    int EmployeeId = reader.GetInt32(0);
                    string Firstname = reader.GetString(1);
                    string lastName = reader.GetString(2);
                    Console.WriteLine($"ID: {EmployeeId}, Name: {Firstname} {lastName}");
                     }
                 }
            } catch (OracleException ex)
            {
                Console.WriteLine($"oracle error {ex.Number}: {ex.Message}");
            }catch (Exception ex)
            {
                Console.WriteLine($"Error viewing employees: {ex.Message} "); 
            }
   
    }
        static void SearchEmployeeID(OracleConnection conn)
        {
            Console.Write("\nEnter Employee ID to search: ");
            string search = Console.ReadLine();

            if (!int.TryParse(search, out int employeeId))
            {
                Console.WriteLine("Invalid Employee ID. Please enter a valid interger.");
                return;
            }
            string sql = "SELECT EmployeeID, FirstName, LastName FROM Employees WHERE EmployeeID = :id";

         try {
                using (OracleCommand cmd = new OracleCommand(sql, conn))
                {
                    cmd.Parameters.Add(new OracleParameter("id", employeeId));

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Console.WriteLine($"ID: {reader.GetInt32(0)}, Name: {reader.GetString(1)} {reader.GetString(2)}");
                        }
                        else
                        {
                            Console.WriteLine($"Employee with ID {employeeId} was not found");
                        }
                    }
                }
            } catch (OracleException ex)
            {
                Console.WriteLine($"Oracle Error {ex.Number}: {ex.Message} ");
            } catch(Exception ex)
            {
                Console.WriteLine($"Error searching employee: {ex.Message}"); 
            }
        }
        static void AddEmployee(OracleConnection conn)
        {


            // firstname
            Console.Write("\nEnter First Name: ");
            string firstName = Console.ReadLine().Trim();
            if (string.IsNullOrEmpty(firstName))
            {
                Console.WriteLine("Invlaid. First Name is Required");
                return;
            }
            //lastname 
            Console.Write("Enter Last Name: ");
            string lastName = Console.ReadLine().Trim();
            if (string.IsNullOrEmpty(lastName))
            {
                Console.WriteLine("Invalid. Last Name is Required");
                return;
            }
            //email 
            Console.Write("Enter Email: ");
            string email = Console.ReadLine().Trim();
            if (!email.Contains("@"))
            {
                Console.WriteLine("Invalid email address");
                return;
            }
            // Phone number
            Console.Write("Enter Phone Number: ");
            string phoneNumber = Console.ReadLine().Trim();
            if (string.IsNullOrEmpty(phoneNumber))
            {
                Console.WriteLine("Phone Number is required");
                return;
            }
            // hire date 
            DateTime hireDate = DateTime.Now;

            // job ID 
            List<string> jobIds = GetJobIDs(conn);
            if (jobIds.Count == 0)
            {
                Console.WriteLine("No Jobs IDs foudn in this database.");
                return;
            }
            Console.WriteLine("Select Job ID: ");
            for (int i = 0; i < jobIds.Count; i++)
                Console.WriteLine($"{i + 1}. {jobIds[i]}");
            Console.WriteLine("Enter the number of your choice:");
            if (!int.TryParse(Console.ReadLine(), out int jobChoice) || jobChoice < 1 || jobChoice > jobIds.Count)
            {
                Console.WriteLine("Invalid Job Id selection");
                return;
            }
            string selectedJobId = jobIds[jobChoice - 1];

            // salary 
            Console.Write("Enter Salary: ");
            string salaryInput = Console.ReadLine().Trim();
            if (!decimal.TryParse(salaryInput, out decimal salary))
            {
                Console.WriteLine("Invalid salary. Please enter a numeric value.");
                return;
            }

            // commission percentage
            Console.Write("Enter Commission Percentage (or leave is blank): ");
            string commissionInput = Console.ReadLine().Trim();
            decimal? commissionPct = null;

            if (!string.IsNullOrEmpty(commissionInput))
            {
                if (decimal.TryParse(commissionInput, out decimal commission))
                {
                    commissionPct = commission;
                }
                else
                {
                    Console.WriteLine("Invalid commision percentage. ");
                    return;
                }
            }

            // Manager ID
            Console.Write("Enter Manager ID (or leave it blank): ");
            string managerInput = Console.ReadLine().Trim();
            int? managerId = null;
            if (!string.IsNullOrEmpty(managerInput))
            {
                if (int.TryParse(managerInput, out int mgrId))
                {
                    managerId = mgrId;
                }
                else
                {
                    Console.WriteLine("Invalid Manager ID.");
                    return;
                }

            }
            // Department ID 
            List<int> deptIds = GetDepartmentIDs(conn);
            if (deptIds.Count == 0)
            {
                Console.WriteLine("No Department IDs found in database");
                return;
            }
            Console.WriteLine("Select Department ID: ");
            for (int i = 0; i < deptIds.Count; i++)
                Console.WriteLine($"{i + 1}. {deptIds[i]}");
            Console.Write("Enter the number of your choice: ");
            if (!int.TryParse(Console.ReadLine(), out int deptChoice) || deptChoice < 1 || deptChoice > deptIds.Count)
            {
                Console.WriteLine("Invalid Department ID selection");
                return;
            }

            int selectedDeptId = deptIds[deptChoice - 1];



            string sql = @"INSERT INTO Employees (FirstName, LastName, Email, PhoneNumber,  HireDate, JobID, Salary, CommissionPct, ManagerID, DepartmentID) 
                           VALUES (:firstName, :lastName, :email, :phoneNumber, :hireDate, :jobId, :salary, :commissionPct, :managerId, :departmentId )";

            using (OracleTransaction transaction = conn.BeginTransaction())

                try
                {


                    using (OracleCommand cmd = new OracleCommand(sql, conn))
                    {
                        cmd.Transaction = transaction;

                        cmd.Parameters.Add(new OracleParameter("firstName", firstName));
                        cmd.Parameters.Add(new OracleParameter("lastName", lastName));
                        cmd.Parameters.Add(new OracleParameter("email", email));
                        cmd.Parameters.Add(new OracleParameter("phoneNumber", phoneNumber));
                        cmd.Parameters.Add(new OracleParameter("hireDate", hireDate));
                        cmd.Parameters.Add(new OracleParameter("jobId", selectedJobId));
                        cmd.Parameters.Add(new OracleParameter("salary", salary));
                        cmd.Parameters.Add(new OracleParameter("commissionPct", commissionPct.HasValue ? (object)commissionPct.Value : DBNull.Value));
                        cmd.Parameters.Add(new OracleParameter("managerId",managerId.HasValue ? (object)managerId : DBNull.Value));
                        cmd.Parameters.Add(new OracleParameter("departmentId", selectedDeptId));



                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            transaction.Commit();
                            Console.WriteLine("Employee added successfully.");
                        }
                        else
                        {
                            transaction.Rollback();
                            Console.WriteLine("Failed to add employee");
                        }
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"Error adding employee: {ex.Message}");
                }
        }
        static void UpdateEmployee(OracleConnection conn)
        {
            Console.Write("\nEnter Employee ID to update: ");
            string input = Console.ReadLine().Trim();
            if (!int.TryParse(input, out int employeeId))
            {
                Console.WriteLine("Invalid Employee ID. enter a valid integer");
                return;
            }

            // checking if the employee even exists 
            string selectSql = "SELECT EmployeeID, FirstName, LastName, Email, PhoneNumber, HireDate, JobID, CommissionPct, ManagerID, DepartmentID, Salary " +
                               "FROM Employees WHERE EmployeeID = :employeeId";
            string currentFirstName = "", currentLastName = "", currentEmail = "", currentPhoneNumber = "", currentJobId = "";
            decimal? currentCommissionPct = null, currentSalary = null;
            int? currentManagerId = null, currentDepartmentId = null;

            using (OracleCommand selectCmd = new OracleCommand(selectSql, conn))
            {
                selectCmd.Parameters.Add(new OracleParameter("employeeId", employeeId));
                using (OracleDataReader reader = selectCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        currentFirstName = reader["FirstName"].ToString();
                        currentLastName = reader["LastName"].ToString();
                        currentEmail = reader["Email"].ToString();
                        currentPhoneNumber = reader["PhoneNumber"].ToString();
                        currentJobId = reader["JobID"].ToString();
                        currentSalary = reader["Salary"] != DBNull.Value ? Convert.ToDecimal(reader["Salary"]) : (decimal?)null;
                        currentCommissionPct = reader["CommissionPct"] != DBNull.Value ? Convert.ToDecimal(reader["CommissionPct"]) : (decimal?)null;
                        currentManagerId = reader["ManagerID"] != DBNull.Value ? Convert.ToInt32(reader["ManagerID"]) : (int?)null;
                        currentDepartmentId = reader["DepartmentID"] != DBNull.Value ? Convert.ToInt32(reader["DepartmentID"]) : (int?)null;

                        Console.WriteLine($"\nCurrent details for Employee ID: {employeeId} ");
                        Console.WriteLine($"First Name: {currentFirstName}");
                        Console.WriteLine($"Last Name: {currentLastName}");
                        Console.WriteLine($"Email: {currentEmail}");
                        Console.WriteLine($"Phone Number: {currentPhoneNumber}");
                        Console.WriteLine($"Job ID: {currentJobId}");
                        Console.WriteLine($"Salary: {currentSalary}");
                        Console.WriteLine($"Commission Pct: {currentCommissionPct}");
                        Console.WriteLine($"Manager ID: {currentManagerId}");
                        Console.WriteLine($"Department ID: {currentDepartmentId}");
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine($"Employee with ID {employeeId} not found");
                        return;
                    }
                }
            }
            Console.WriteLine("\nEnter new values(Leave blank to keep the current value)");

            // Helps get the new value or keep the old one
            string Prompt(string label, string current)
            {
                while (true)
                {
                    Console.Write($"Enter new {label} (current '{current}'), leave blank to keep: ");
                    string value = Console.ReadLine().Trim();
                    if (value == "") return current;
                    if (label == "Email" && (!value.Contains("@") || !value.Contains(".")))
                    {
                        Console.WriteLine("invalid email format.");
                        continue;
                    }
                    return value;
                }
            }

            //the input collection wiht the validations
            string newFirstname = Prompt("First Name", currentFirstName);
            string newLastName = Prompt("Last Name", currentLastName);
            string newEmail = Prompt("Email", currentEmail);
            string newPhoneNumber = Prompt("Phone Number", currentPhoneNumber); // this assusmes phone number can be empty for the update. 

            //job ID dropdown 
            List<string> jobIds = GetJobIDs(conn);
            string newJobId = currentJobId;
            while (true)
            {
                Console.WriteLine($"\nSelect new Job ID (current: '{currentJobId}'. Enter 0 to keep the current Job ID)");

                for (int i = 0; i < jobIds.Count; i++)

                    Console.WriteLine($"{i + 1}. {jobIds[i]}");
                Console.Write("Enter the number of your choice. (Enter 0 to keep current): ");

                string choice = Console.ReadLine().Trim();

                if (choice == "0")
                {
                    newJobId = currentJobId;
                    break;
                }
                if (int.TryParse(choice, out int index) && index >= 1 && index <= jobIds.Count)
                {
                    newJobId = jobIds[index - 1];
                    break;
                }
                Console.WriteLine("Invalid selection. Try again.");

            }

            //Salary 
            decimal? newSalary = currentSalary;
            while (true)
            {
                Console.Write($"Enter new Salary (current: '{currentSalary}'. Leave blank to keep): ");
                string salaryInput = Console.ReadLine().Trim();
                if (salaryInput == "") break;
                if (decimal.TryParse(salaryInput, out decimal value))
                {
                    newSalary = value;
                    break;
                }
                Console.WriteLine("invalid salary. Please enter a numeric value");
            }


            // Commission Pact (optional) 
            decimal? newCommissionPct = currentCommissionPct;
            while (true)
            {
                Console.Write($"Enter a new Commission Percentage (current: '{currentCommissionPct}'. Leave blank to keep): ");
                string commissionInput = Console.ReadLine().Trim();
                if (commissionInput == "") break;
                if (decimal.TryParse(commissionInput, out decimal value))
                {
                    newCommissionPct = value;
                    break;
                }
                Console.WriteLine("invalid commission percentage. Please enter a numeric value");
            }


            // Manager ID(optional) 
            int? newManagerId = currentManagerId;
            while (true)
            {
                Console.Write($"Enter a new Manager ID (current: '{currentManagerId}'. Leave blank to keep) ");
                string mgrInput = Console.ReadLine().Trim();
                if (mgrInput == "") break;
                if (int.TryParse(mgrInput, out int value))
                {
                    newManagerId = value;
                    break;
                }
                Console.WriteLine("invalid Manager ID. Please enter a numeric value");
            }

            //Department ID (dropdown)
            List<int> deptIds = GetDepartmentIDs(conn);
            int? newDepartmentId = currentDepartmentId;
            while (true)
            {
                Console.WriteLine($"\nSelect new Department ID (current: '{currentDepartmentId}'. Enter 0 to keep the current Job ID)");

                for (int i = 0; i < deptIds.Count; i++)

                    Console.WriteLine($"{i + 1}. {deptIds[i]}");
                Console.Write("Enter the number of your choice. (Enter 0 to keep current): ");

                string choice = Console.ReadLine().Trim();

                if (choice == "0")
                {
                    newDepartmentId = currentDepartmentId;
                    break;
                }
                if (int.TryParse(choice, out int index) && index >= 1 && index <= deptIds.Count)
                {
                    newDepartmentId = deptIds[index - 1];
                    break;
                }
                Console.WriteLine("Invalid selection. Try again.");

            }



            // Construct UPDATE statement dynamically 
            // ONLY update fields that were changed by the user (not left blank)
            StringBuilder updateSql = new StringBuilder("UPDATE Employees SET ");
            List<OracleParameter> parameters = new List<OracleParameter>();
            bool firstParameter = true;

            void AddField(string col, object value, object oldValue)
            {
                if (!object.Equals(value, oldValue))
                {
                    if (!firstParameter) updateSql.Append(", ");
                    updateSql.Append($"{col} = :{col}");
                    parameters.Add(new OracleParameter(col, value ?? DBNull.Value));
                    firstParameter = false;
                }
            }
            AddField("FirstName", newFirstname, currentFirstName);
            AddField("LastName", newLastName, currentLastName);
            AddField("Email", newEmail, currentEmail);
            AddField("PhoneNumber", newPhoneNumber, currentPhoneNumber);
            AddField("JobID", newJobId, currentJobId);
            AddField("Salary", newSalary, currentSalary);
            AddField("CommissionPct", newCommissionPct, currentCommissionPct);
            AddField("ManagerID", newManagerId, currentManagerId);
            AddField("DepartmentID", newDepartmentId, currentDepartmentId);


            if (firstParameter) // no field were updatated
            {
                Console.WriteLine("No changes were detected or there is an invalid input. Employee not updated");
                return;
            }

            updateSql.Append(" WHERE EmployeeID = :employeeId");
            parameters.Add(new OracleParameter("employeeId", employeeId));

            // to catch any exceptions 
            using (OracleTransaction transaction = conn.BeginTransaction())
            {
                try
                {
                    using (OracleCommand cmd = new OracleCommand(updateSql.ToString(), conn))
                    {
                        cmd.Transaction = transaction;
                        cmd.Parameters.AddRange(parameters.ToArray());

                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            transaction.Commit();
                            Console.WriteLine("Employee updated successfully.");
                        }
                        else
                        {
                            transaction.Rollback();
                            Console.WriteLine("Employee not found or no chnages were made");
                        }
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"Error updating employee: {ex.Message}");
                }
            }
        }



        static void DeleteEmployee(OracleConnection conn)
        {
            Console.Write("\nEnter Employee ID to delete: ");
            string input = Console.ReadLine().Trim();

            if (!int.TryParse(input, out int employeeId))
            {
                Console.WriteLine("invalid Employee ID. Enter a valid integer");
                return;
            }
            // this displays the employee details before delete is confirmed

            string checkSql = "SELECT Firstname, LastName FROM Employees WHERE EmployeeID = :employeeId";

            string employeeName = null;

            using (OracleCommand checkCmd = new OracleCommand(checkSql, conn))
            {
                checkCmd.Parameters.Add(new OracleParameter("employeeId", employeeId));
                using (OracleDataReader reader = checkCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        employeeName = $"{reader.GetString(0)} {reader.GetString(1)}";
                        Console.Write($"\nAre you sure you want to delete employee: {employeeName} (ID: {employeeId})? (Y/N): ");
                        string confirm = Console.ReadLine().Trim().ToUpper();
                        if (confirm != "Y")
                        {
                            Console.WriteLine("Deletion cancelled.");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Employee with ID {employeeId} not found.");
                        return;
                    }
                }
            }

            string sql = "DELETE FROM Employees WHERE EmployeeID = :employeeID";

            using (OracleTransaction transaction = conn.BeginTransaction())
            {
                try
                {
                    using (OracleCommand cmd = new OracleCommand(sql, conn))
                    {
                        cmd.Transaction = transaction;
                        cmd.Parameters.Add(new OracleParameter("employeeID", employeeId));

                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            transaction.Commit();
                            Console.WriteLine("Employee deleted successfully.");
                        }else 
                        {
                            transaction.Rollback();
                            Console.WriteLine("Employee not found."); 
                        }
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback(); 
                    Console.WriteLine($"Error deleting employee: {ex.Message}");
                    // Consider specific errro handling, e.g. if the employee was referenced by a foreign key
                }
            }
        }
    }

}
