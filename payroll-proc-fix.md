# Payroll Proc Fix: Deductions Gap Solved 

## The Mistake
**Original `CalculateEmployeePayroll` logic** skipped deductions for **new payrolls**:

```sql
-- OLD CODE PROBLEM:
  BEGIN
        SELECT PayrollID INTO v_PayrollID
        FROM Payroll
        WHERE EmployeeID = p_EmployeeID
          AND PayPeriodStart = p_PayPeriodStart
          AND PayPeriodEnd = p_PayPeriodEnd;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            v_PayrollID := NULL;
    END;

    -- Calculate deductions only if payroll record exists
    IF v_PayrollID IS NOT NULL THEN
        SELECT NVL(SUM(CASE WHEN d.DeductionType = 'Percentage' THEN pd.DeductionAmount ELSE 0 END), 0),
               NVL(SUM(CASE WHEN d.DeductionType = 'Fixed' THEN pd.DeductionAmount ELSE 0 END), 0)
        INTO v_TaxDeductions, v_OtherDeductions
        FROM PayrollDeductions pd
        JOIN Deductions d ON pd.DeductionID = d.DeductionID
        WHERE pd.PayrollID = v_PayrollID;
    END IF;

    -- Calculate net salary
    v_NetSalary := v_GrossSalary - v_TaxDeductions - v_OtherDeductions - v_Deductions;

    -- Insert or update Payroll record
    IF v_PayrollID IS NULL THEN
        INSERT INTO Payroll (EmployeeID, PayPeriodStart, PayPeriodEnd, GrossSalary, TaxDeductions, OtherDeductions, NetSalary, PaymentDate)
        VALUES (p_EmployeeID, p_PayPeriodStart, p_PayPeriodEnd, v_GrossSalary, v_TaxDeductions, v_OtherDeductions, v_NetSalary, SYSDATE);
    ELSE
        UPDATE Payroll
        SET GrossSalary = v_GrossSalary,
            TaxDeductions = v_TaxDeductions,
            OtherDeductions = v_OtherDeductions,
            NetSalary = v_NetSalary,
            PaymentDate = SYSDATE
        WHERE PayrollID = v_PayrollID;
    END IF;


    COMMIT; 
    EXCEPTION WHEN OTHERS THEN DBMS_OUTPUT.PUT_LINE('Error: ' || SQLERRM); ROLLBACK;
END CalculateEmployeePayroll;
/

```
Result: First-time payroll runs → TaxDeductions=0, OtherDeductions=0. No PayrollDeductions links
Before:
Employee 3 → June 2025 → Gross=65k, Tax=0, Net=65k ❌
After:
Employee 3 → June 2025 → Gross=65k, Tax=9.75k, Net=55.25k ✅
PayrollDeductions table populated

had to remember to always INSERT Payroll record first THEN calculate/insert deductions using that ID, so you I
UPDATE Payroll with totals. NO GAAAPS. 


```sql
-- **NEW VERSION: ALWAYS INSERT Payroll first**
    INSERT INTO Payroll (EmployeeID, PayPeriodStart, PayPeriodEnd, GrossSalary, TaxDeductions, OtherDeductions, NetSalary, PaymentDate)
    VALUES (p_EmployeeID, p_PayPeriodStart, p_PayPeriodEnd, v_GrossSalary, 0, 0, 0, SYSDATE)
    RETURNING PayrollID INTO v_PayrollID;

    -- **NEW VERSION: NOW calculate and INSERT deductions for this PayrollID**
    INSERT INTO PayrollDeductions (PayrollID, DeductionID, DeductionAmount)
    SELECT v_PayrollID, d.DeductionID, 
           CASE 
               WHEN d.DeductionType = 'Percentage' THEN v_GrossSalary * (d.DeductionValue / 100)
               WHEN d.DeductionType = 'Fixed' THEN d.DeductionValue 
           END
    FROM Deductions d
    WHERE d.IsMandatory = 'Y';  -- Only mandatory ones

    -- **NEW VERSION: Sum the deductions we just inserted**
    SELECT NVL(SUM(CASE WHEN d.DeductionType = 'Percentage' THEN pd.DeductionAmount ELSE 0 END), 0),
           NVL(SUM(CASE WHEN d.DeductionType = 'Fixed' THEN pd.DeductionAmount ELSE 0 END), 0)
    INTO v_TaxDeductions, v_OtherDeductions
    FROM PayrollDeductions pd
    JOIN Deductions d ON pd.DeductionID = d.DeductionID
    WHERE pd.PayrollID = v_PayrollID;

    -- Calculate net
    v_NetSalary := v_GrossSalary - v_TaxDeductions - v_OtherDeductions - v_Deductions;

    -- **UPDATE Payroll with final totals**
    UPDATE Payroll
    SET TaxDeductions = v_TaxDeductions,
        OtherDeductions = v_OtherDeductions,
        NetSalary = v_NetSalary,
        PaymentDate = SYSDATE
    WHERE PayrollID = v_PayrollID;

    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error: ' || SQLERRM);
        ROLLBACK;
END CalculateEmployeePayroll;
/
```
Don't worry te batch proc remains the same. 
Now the new payroll and the deductions a linked automatically. 
The deductions now work on first run too yey.. 

REMEMBER TO TEST THE RESULTS

EXEC CalculateEmployeePayroll(3, '2025-07-01', '2025-07-15');

SELECT * FROM Payroll WHERE EmployeeID=3 ORDER BY PayPeriodStart DESC;
SELECT * FROM PayrollDeductions WHERE PayrollID = [new ID];


