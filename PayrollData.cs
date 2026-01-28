using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;


namespace PayrollApplication
{
    public class PayrollData
    {

        public int EmployeeID { get; set; }
        public decimal Salary { get; set; }
        public int DepartmentID { get; set; }
        public string JobID { get; set; }
        public int? ManagerID { get; set; }
       
    }

}
