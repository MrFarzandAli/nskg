CREATE OR ALTER PROCEDURE [dbo].[PROCESSDETAIL_PartyExp]  
(
    @CompanyId int,
    @TDATE  DATE,
    @ACCODE VARCHAR(50) = '',
    @VehicleNo VARCHAR(50) = ''
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @SDATE DATE = DATEFROMPARTS(YEAR(@TDATE), 1, 1);

    BEGIN TRY

        SELECT TOP 1 @SDATE = StartDate
        FROM FinancialYears
        WHERE (CompanyId = @CompanyId OR @CompanyId = 0)
          AND @TDATE >= StartDate AND @TDATE <= EndDate;

        DELETE FROM ACCUMULATED;
        DELETE FROM ACCOPEN;

        IF @ACCODE IS NULL SET @ACCODE = '';
        IF @VehicleNo IS NULL SET @VehicleNo = '';
        SET @ACCODE = RTRIM(LTRIM(@ACCODE));
        SET @VehicleNo = RTRIM(LTRIM(@VehicleNo));

        INSERT INTO ACCOPEN (AC1, AC3)
        SELECT
            SUBSTRING(ACC, 1, 3),
            SUBSTRING(ACC, 4, 3)
        FROM GLCHART3
        WHERE (CompanyId = @CompanyId OR CompanyId = 0 OR CompanyId IS NULL)
          AND (@ACCODE = '' OR RTRIM(AC1 + AC3) = @ACCODE OR RTRIM(ACC) = @ACCODE);

        -- 1. VoDet
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, DRAMT, CRAMT,
            NARRATION, CHQNO, CHQDATE,
            BILLTINO, BILNO, VEHICLENO, QTY
        )
        SELECT
            D.COCODE, D.VONO, D.VODATE, D.VOTYPE,
            D.AC1, D.AC3, D.DRAMT, D.CRAMT,
            D.NARRATION, D.CHQNO, D.CHQDATE,
            D.BILLTINO, D.BILNO, D.VEHICLENO, D.QTY
        FROM VODET D
        WHERE D.VODATE <= @TDATE
          AND (D.COCODE = (SELECT TOP 1 COCODE FROM COMPANIES WHERE Id = @CompanyId) OR D.COCODE = CAST(@CompanyId AS VARCHAR) OR @CompanyId = 0)
          AND (@ACCODE = '' OR RTRIM(D.AC1 + D.AC3) = @ACCODE OR RTRIM(D.ACC) = @ACCODE)
          AND (@VehicleNo = '' 
               OR RTRIM(D.VEHICLENO) = @VehicleNo 
               OR D.VEHICLENO LIKE '%' + @VehicleNo + '%'
               OR REPLACE(REPLACE(ISNULL(D.VEHICLENO, ''), '-', ''), ' ', '') = REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '')
               OR REPLACE(REPLACE(ISNULL(D.VEHICLENO, ''), '-', ''), ' ', '') LIKE '%' + REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '') + '%')
          AND ISNULL(D.IsDeleted, 0) = 0;

        -- 2. CommHead Advance (Debit)
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, DRAMT, CRAMT,
            NARRATION, STATION, VEHICLENO, BILLTINO, BILNO, INVNO
        )
        SELECT
            ISNULL((SELECT TOP 1 Cocode FROM Companies WHERE Id = H.CompanyId), '01'),
            H.DOCNO, H.DOCDATE, 'AD',
            SUBSTRING(COALESCE(NULLIF(RTRIM(H.ADVANCECODE), ''), g.ACC, g.AC1+g.AC3, ''), 1, 3),
            SUBSTRING(COALESCE(NULLIF(RTRIM(H.ADVANCECODE), ''), g.ACC, g.AC1+g.AC3, ''), 4, 3),
            ISNULL(H.ADVANCEAMT, 0),
            0,
            COALESCE(NULLIF(RTRIM(H.NARRATION), ''), 'COMMISSION BOOK ADVANCE'),
            H.STATION,
            H.VEHICLENO,
            (SELECT TOP 1 cmd.BillTiNo FROM CommDetail cmd WHERE cmd.CommHeadId = H.Id),
            H.CHALNO,
            CAST(H.CHALNO AS VARCHAR(10))
        FROM COMMHEAD H
        LEFT JOIN GLCHART3 g ON g.Id = H.AdvanceId
        WHERE (@ACCODE = '' 
               OR RTRIM(H.ADVANCECODE) = @ACCODE 
               OR RTRIM(g.ACC) = @ACCODE 
               OR RTRIM(g.AC1 + g.AC3) = @ACCODE)
          AND (@VehicleNo = '' 
               OR RTRIM(H.VEHICLENO) = @VehicleNo 
               OR H.VEHICLENO LIKE '%' + @VehicleNo + '%'
               OR REPLACE(REPLACE(ISNULL(H.VEHICLENO, ''), '-', ''), ' ', '') = REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '')
               OR REPLACE(REPLACE(ISNULL(H.VEHICLENO, ''), '-', ''), ' ', '') LIKE '%' + REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '') + '%')
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.ADVANCEAMT, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0
          AND (H.CompanyId = @CompanyId OR @CompanyId = 0);

        -- 3. Challan Liability 1 (Credit)
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, CRAMT, ACTYPE,
            NARRATION, STATION, VEHICLENO, BILLTINO, INVNO
        )
        SELECT
            ISNULL((SELECT TOP 1 Cocode FROM Companies WHERE Id = H.CompanyId), '01'),
            H.DOCNO, H.DOCDATE, 'CL',
            SUBSTRING(H.PEXPCODE, 1, 3),
            SUBSTRING(H.PEXPCODE, 4, 3),
            ISNULL(H.PEXPAMT, 0), 'L',
            H.VEHICLENO,
            H.STATION + ' Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''),
            H.VEHICLENO,
            H.PEXPBILTI, ''
        FROM CHALLANHEAD H
        WHERE (@ACCODE = '' OR RTRIM(H.PEXPCODE) = @ACCODE)
          AND (@VehicleNo = '' 
               OR RTRIM(H.VEHICLENO) = @VehicleNo 
               OR H.VEHICLENO LIKE '%' + @VehicleNo + '%'
               OR REPLACE(REPLACE(ISNULL(H.VEHICLENO, ''), '-', ''), ' ', '') = REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '')
               OR REPLACE(REPLACE(ISNULL(H.VEHICLENO, ''), '-', ''), ' ', '') LIKE '%' + REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '') + '%')
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0
          AND (H.CompanyId = @CompanyId OR @CompanyId = 0);

        -- 4. Challan Liability 2 (Credit)
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, CRAMT, ACTYPE,
            NARRATION, STATION, VEHICLENO, BILLTINO, INVNO
        )
        SELECT
            ISNULL((SELECT TOP 1 Cocode FROM Companies WHERE Id = H.CompanyId), '01'),
            H.DOCNO, H.DOCDATE, 'CL',
            SUBSTRING(H.PEXPCODE2, 1, 3),
            SUBSTRING(H.PEXPCODE2, 4, 3),
            ISNULL(H.PEXPAMT2, 0), 'L',
            H.VEHICLENO,
            H.STATION + ' Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''),
            H.VEHICLENO,
            H.PEXPBILTI2, ''
        FROM CHALLANHEAD H
        WHERE (@ACCODE = '' OR RTRIM(H.PEXPCODE2) = @ACCODE)
          AND (@VehicleNo = '' 
               OR RTRIM(H.VEHICLENO) = @VehicleNo 
               OR H.VEHICLENO LIKE '%' + @VehicleNo + '%'
               OR REPLACE(REPLACE(ISNULL(H.VEHICLENO, ''), '-', ''), ' ', '') = REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '')
               OR REPLACE(REPLACE(ISNULL(H.VEHICLENO, ''), '-', ''), ' ', '') LIKE '%' + REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '') + '%')
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT2, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0
          AND (H.CompanyId = @CompanyId OR @CompanyId = 0);

        -- 5. Challan Liability 3 (Credit)
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, CRAMT, ACTYPE,
            NARRATION, STATION, VEHICLENO, BILLTINO, INVNO
        )
        SELECT
            ISNULL((SELECT TOP 1 Cocode FROM Companies WHERE Id = H.CompanyId), '01'),
            H.DOCNO, H.DOCDATE, 'CL',
            SUBSTRING(H.PEXPCODE3, 1, 3),
            SUBSTRING(H.PEXPCODE3, 4, 3),
            ISNULL(H.PEXPAMT3, 0), 'L',
            H.VEHICLENO,
            H.STATION + ' Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''),
            H.VEHICLENO,
            H.PEXPBILTI3, ''
        FROM CHALLANHEAD H
        WHERE (@ACCODE = '' OR RTRIM(H.PEXPCODE3) = @ACCODE)
          AND (@VehicleNo = '' 
               OR RTRIM(H.VEHICLENO) = @VehicleNo 
               OR H.VEHICLENO LIKE '%' + @VehicleNo + '%'
               OR REPLACE(REPLACE(ISNULL(H.VEHICLENO, ''), '-', ''), ' ', '') = REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '')
               OR REPLACE(REPLACE(ISNULL(H.VEHICLENO, ''), '-', ''), ' ', '') LIKE '%' + REPLACE(REPLACE(@VehicleNo, '-', ''), ' ', '') + '%')
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT3, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0
          AND (H.CompanyId = @CompanyId OR @CompanyId = 0);


        UPDATE AO
        SET OPENING =
        (
            SELECT SUM(ISNULL(A.DRAMT, 0) - ISNULL(A.CRAMT, 0))
            FROM ACCUMULATED A
            WHERE A.VODATE < @SDATE
        )
        FROM ACCOPEN AO;

        RETURN 0;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;
END;