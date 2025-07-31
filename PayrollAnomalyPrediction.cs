using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.Data;
using Oracle.ManagedDataAccess.Client;

namespace PayrollApplication
{
    public class PayrollAnomalyPrediction
    {
        public float EmployeeID;

        [ColumnName("Score")]
        public float AnomalyScore;
    }
}
