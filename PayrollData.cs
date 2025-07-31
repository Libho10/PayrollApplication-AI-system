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

        public float EmployeeID { get; set; }
        public float Salary { get; set; }
        public float CommissionPct { get; set; }
        public float DepartmentID { get; set; }
        public string JobID { get; set; }
        public float ManagerID { get; set; }
       
    }

}
