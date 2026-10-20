-- =========================================================================
-- Stored Procedure: dbo.PROCESSDETAIL_PartyExp
-- Converted directly from Oracle PROCESSDETAIL procedure for Party Expense
-- Supports multi-branch (@CompanyId = 0) and linked accounts
-- =========================================================================
CREATE OR ALTER PROCEDURE [dbo].[PROCESSDETAIL_PartyExp]  
(
    @CompanyId int = 0,
    @TDATE  DATE,
    @ACCODE VARCHAR(50) = '',
    @VehicleNo VARCHAR(50) = ''
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @MDATE DATE,
        @MON CHAR(2),
        @SYEAR CHAR(2),
        @SDATE DATE,
        @FYEAR CHAR(1) = 'H';

    BEGIN TRY
        SET @ACCODE = LTRIM(RTRIM(ISNULL(@ACCODE, '')));
        SET @VehicleNo = LTRIM(RTRIM(ISNULL(@VehicleNo, '')));

        SET @MDATE = @TDATE;
        SET @MON = RIGHT('0' + CAST(MONTH(@MDATE) AS VARCHAR(2)), 2);

        /* =========================================================
           CALCULATE START DATE (SDATE)
           ========================================================= */
        IF @FYEAR = 'H'
        BEGIN
            IF @MON BETWEEN '07' AND '12'
            BEGIN
                SET @SYEAR = RIGHT(CAST(YEAR(@MDATE) AS VARCHAR(4)), 2);
            END
            ELSE
            BEGIN
                SET @SYEAR = RIGHT('0' + CAST((YEAR(@MDATE) % 100) - 1 AS VARCHAR(2)), 2);
            END;

            SET @SDATE = DATEFROMPARTS(
                CASE WHEN @MON BETWEEN '07' AND '12' THEN YEAR(@MDATE) ELSE YEAR(@MDATE) - 1 END,
                7, 1);
        END
        ELSE
        BEGIN
            SET @SDATE = DATEFROMPARTS(YEAR(@MDATE), 1, 1);
        END;

        /* =========================================================
           RESOLVE LINKED ACCOUNT CODES (IF ANY)
           ========================================================= */
        DECLARE @TargetAccounts TABLE (AccCode VARCHAR(50));

        IF @ACCODE <> ''
        BEGIN
            INSERT INTO @TargetAccounts (AccCode)
            SELECT DISTINCT RTRIM(g2.AC1) + RTRIM(g2.AC3)
            FROM GLCHART3 g1
            JOIN GLCHART3 g2 ON (g1.LinkedGroupId IS NOT NULL AND g2.LinkedGroupId = g1.LinkedGroupId)
                             OR (g1.LinkedGroupId IS NULL AND (RTRIM(g2.AC1) + RTRIM(g2.AC3) = RTRIM(g1.AC1) + RTRIM(g1.AC3) OR RTRIM(g2.ACC) = RTRIM(g1.ACC)))
            WHERE RTRIM(g1.AC1) + RTRIM(g1.AC3) = @ACCODE OR RTRIM(g1.ACC) = @ACCODE;

            IF NOT EXISTS (SELECT 1 FROM @TargetAccounts)
            BEGIN
                INSERT INTO @TargetAccounts (AccCode) VALUES (@ACCODE);
            END;
        END;

        /* =========================================================
           CLEAR ACCUMULATION TABLES
           ========================================================= */
        DELETE FROM ACCUMULATED;
        DELETE FROM ACCOPEN;

        /* =========================================================
           ACCOUNT OPENING
           ========================================================= */
        INSERT INTO ACCOPEN (AC1, AC3, ACTYPE, COCODE)
        SELECT AC1, AC3, ACTYPE, CompanyId
        FROM GLCHART3
        WHERE (@CompanyId = 0 OR CompanyId = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(AC1) + RTRIM(AC3) IN (SELECT AccCode FROM @TargetAccounts) OR RTRIM(ACC) IN (SELECT AccCode FROM @TargetAccounts))
          AND CTYPE <> '4';

        /* =========================================================
           ACPARA CONTROL ACCOUNTS UPDATES (Single Company Only)
           ========================================================= */
        IF @CompanyId > 0
        BEGIN
            UPDATE ISSHEAD
            SET ACcCODE = (SELECT TOP 1 RTRIM(ACCODE) FROM ACPARA WHERE ACTYPE = 'S' AND CompanyId = @CompanyId)
            WHERE CompanyId = @CompanyId;

            UPDATE COMMHEAD
            SET ACCODE = (SELECT TOP 1 RTRIM(ACCODE) FROM ACPARA WHERE ACTYPE = 'D' AND CompanyId = @CompanyId)
            WHERE CompanyId = @CompanyId;

            UPDATE ISSDETAIL
            SET ACcCODE = (SELECT TOP 1 RTRIM(ACCODE) FROM ACPARA WHERE ACTYPE = 'S' AND CompanyId = @CompanyId)
            WHERE CompanyId = @CompanyId;

            UPDATE CommDetail
            SET ACCODE = (SELECT TOP 1 RTRIM(ACCODE) FROM ACPARA WHERE ACTYPE = 'D' AND CompanyId = @CompanyId)
            WHERE CompanyId = @CompanyId;
        END;

        /* =========================================================
           1. VODET (HACC = @ACCODE, INVNO = '0') -> Contra Offset
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, CRAMT,
            NARRATION, CHQNO, CHQDATE, ACTYPE,
            INVNO, BILLTINO, BILNO, INAME, VEHICLENO, STATION
        )
        SELECT
            h.CompanyId, d.VONO, d.VODATE, d.VOTYPE,
            SUBSTRING(d.HACC, 1, 3), SUBSTRING(d.HACC, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE d.HACC END,
            d.CRAMT, d.DRAMT,
            RTRIM(d.NARRATION), d.CHQNO, d.CHQDATE, '2',
            d.INVNO, d.BILLTINO, d.BILNO, d.NAME, d.VEHICLENO, d.TRANSPORTER
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE (@CompanyId = 0 OR h.CompanyId = @CompanyId)
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') = '0'
          AND (@ACCODE = '' OR ISNULL(d.HACC, '999999') IN (SELECT AccCode FROM @TargetAccounts))
          AND d.ACC <> ISNULL(d.HACC, '999999')
          AND ISNULL(h.IsDeleted, 0) = 0;

        /* =========================================================
           2. VODET INVOICE OFFSET (HACC = @ACCODE, INVNO != '0')
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, CRAMT,
            NARRATION, CHQNO, CHQDATE, ACTYPE
        )
        SELECT
            h.CompanyId, d.VONO, d.VODATE, d.VOTYPE,
            SUBSTRING(d.HACC, 1, 3), SUBSTRING(d.HACC, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE d.HACC END,
            SUM(ISNULL(d.CRAMT, 0)), SUM(ISNULL(d.DRAMT, 0)),
            RTRIM(d.NARRATION), d.CHQNO, d.CHQDATE, '2'
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE (@CompanyId = 0 OR h.CompanyId = @CompanyId)
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') <> '0'
          AND (@ACCODE = '' OR ISNULL(d.HACC, '999999') IN (SELECT AccCode FROM @TargetAccounts))
          AND d.ACC <> ISNULL(d.HACC, '999999')
          AND ISNULL(h.IsDeleted, 0) = 0
        GROUP BY h.CompanyId, d.VONO, d.VODATE, d.VOTYPE, SUBSTRING(d.HACC, 1, 3), SUBSTRING(d.HACC, 4, 3), d.HACC, RTRIM(d.NARRATION), d.CHQNO, d.CHQDATE;

        /* =========================================================
           3. VODET DETAIL LINE (AC1||AC3 = @ACCODE, INVNO = '0')
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, CRAMT,
            NARRATION, CHQNO, CHQDATE, ACTYPE,
            INVNO, BILLTINO, BILNO, VEHICLENO, INAME
        )
        SELECT
            h.CompanyId, d.VONO, d.VODATE, d.VOTYPE,
            d.AC1, d.AC3,
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE RTRIM(d.AC1) + RTRIM(d.AC3) END,
            d.DRAMT, d.CRAMT,
            RTRIM(d.NARRATION), d.CHQNO, d.CHQDATE, '2',
            d.INVNO, d.BILLTINO, d.BILNO, d.VEHICLENO, d.NARRATION
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE (@CompanyId = 0 OR h.CompanyId = @CompanyId)
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') = '0'
          AND (ISNULL(d.DRAMT, 0) + ISNULL(d.CRAMT, 0) <> 0)
          AND ISNULL(d.HACC, '999999') NOT IN (SELECT AccCode FROM @TargetAccounts)
          AND (@ACCODE = '' OR RTRIM(d.AC1) + RTRIM(d.AC3) IN (SELECT AccCode FROM @TargetAccounts) OR RTRIM(d.ACC) IN (SELECT AccCode FROM @TargetAccounts))
          AND (RTRIM(d.AC1) + RTRIM(d.AC3)) <> ISNULL(d.HACC, '999999')
          AND ISNULL(h.IsDeleted, 0) = 0;

        /* =========================================================
           4. VODET TAX DETECTION (PTAX <> 0)
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, INAME,
            CHQNO, CHQDATE, ACTYPE,
            INVNO, BILLTINO, BILNO
        )
        SELECT
            h.CompanyId, d.VONO, d.VODATE, 'TD',
            d.AC1, d.AC3,
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE RTRIM(d.AC1) + RTRIM(d.AC3) END,
            d.PTAX, 'Tax Detection',
            d.CHQNO, d.CHQDATE, '2',
            d.INVNO, d.BILLTINO, d.BILNO
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE (@CompanyId = 0 OR h.CompanyId = @CompanyId)
          AND d.VODATE <= @TDATE
          AND ISNULL(d.PTAX, 0) <> 0
          AND ISNULL(d.INVNO, '0') = '0'
          AND ISNULL(d.HACC, '999999') NOT IN (SELECT AccCode FROM @TargetAccounts)
          AND (@ACCODE = '' OR RTRIM(d.AC1) + RTRIM(d.AC3) IN (SELECT AccCode FROM @TargetAccounts) OR RTRIM(d.ACC) IN (SELECT AccCode FROM @TargetAccounts))
          AND (RTRIM(d.AC1) + RTRIM(d.AC3)) <> ISNULL(d.HACC, '999999')
          AND ISNULL(h.IsDeleted, 0) = 0;

        /* =========================================================
           5. VODET DETAIL LINE INVOICE OFFSET (INVNO != '0')
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, CRAMT,
            NARRATION, CHQNO, CHQDATE, ACTYPE
        )
        SELECT
            h.CompanyId, d.VONO, d.VODATE, d.VOTYPE,
            d.AC1, d.AC3,
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE RTRIM(d.AC1) + RTRIM(d.AC3) END,
            SUM(ISNULL(d.DRAMT, 0)), SUM(ISNULL(d.CRAMT, 0)),
            RTRIM(d.NARRATION), d.CHQNO, d.CHQDATE, '2'
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE (@CompanyId = 0 OR h.CompanyId = @CompanyId)
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') <> '0'
          AND ISNULL(d.HACC, '999999') NOT IN (SELECT AccCode FROM @TargetAccounts)
          AND (@ACCODE = '' OR RTRIM(d.AC1) + RTRIM(d.AC3) IN (SELECT AccCode FROM @TargetAccounts) OR RTRIM(d.ACC) IN (SELECT AccCode FROM @TargetAccounts))
          AND (RTRIM(d.AC1) + RTRIM(d.AC3)) <> ISNULL(d.HACC, '999999')
          AND ISNULL(h.IsDeleted, 0) = 0
        GROUP BY h.CompanyId, d.VONO, d.VODATE, d.VOTYPE, d.AC1, d.AC3, RTRIM(d.NARRATION), d.CHQNO, d.CHQDATE;

        /* =========================================================
           6. ITEM WISE SALES PAID (ISSHEAD 'BL')
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, ACTYPE,
            NARRATION, QTY, BILLTINO, BILNO, STATION
        )
        SELECT
            H.CompanyId, H.DOCNO, H.DOCDATE, 'BL',
            SUBSTRING(H.CUSCODE, 1, 3), SUBSTRING(H.CUSCODE, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.CUSCODE END,
            ISNULL(H.NETAMT, 0), '3',
            H.VEHICLENO, H.QTY, H.BILLTINO, H.BILNO, H.FOODER
        FROM ISSHEAD H
        WHERE (@CompanyId = 0 OR H.CompanyId = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.CUSCODE) IN (SELECT AccCode FROM @TargetAccounts))
          AND RTRIM(H.PTYPE) = 'Paid'
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           7. TRANSPORTER FREIGHT (COMMHEAD 'TR')
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, ACTYPE,
            NARRATION, INAME, NCRAMT, NDRAMT, AMOUNT
        )
        SELECT
            H.CompanyId, H.DOCNO, H.DOCDATE, 'TR',
            SUBSTRING(H.TRANSCODE, 1, 3), SUBSTRING(H.TRANSCODE, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.TRANSCODE END,
            ISNULL(H.TRANSPORTERAMT, 0), '3',
            H.VEHICLENO, H.STATION, H.TOTNET, H.LOCALAMT, H.DELIVERYAMT
        FROM COMMHEAD H
        WHERE (@CompanyId = 0 OR H.CompanyId = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.TRANSCODE) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           8. SALES ACCOUNT (ISSHEAD 'BL' ACcCODE)
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE,
            NARRATION, QTY, INAME, STATION, BILNO, BILLTINO
        )
        SELECT
            H.CompanyId, H.DOCNO, H.DOCDATE, 'BL',
            SUBSTRING(H.ACcCODE, 1, 3), SUBSTRING(H.ACcCODE, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.ACcCODE END,
            ISNULL(H.NETAMT, 0), '3',
            H.VEHICLENO, H.QTY, H.CUSNAME, H.FOODER, H.BILNO, H.BILLTINO
        FROM ISSHEAD H
        WHERE (@CompanyId = 0 OR H.CompanyId = @CompanyId)
          AND (@ACCODE = '' OR H.ACcCODE IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           9. COMMISSION BOOK (COMMHEAD 'CB' POSITIVE TOTAMT -> CRAMT)
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE,
            NARRATION, STATION
        )
        SELECT
            H.CompanyId, H.DOCNO, H.DOCDATE, 'CB',
            SUBSTRING(H.ACCODE, 1, 3), SUBSTRING(H.ACCODE, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.ACCODE END,
            ISNULL(H.TOTAMT, 0), '3',
            H.VEHICLENO, H.STATION
        FROM COMMHEAD H
        WHERE (@CompanyId = 0 OR H.CompanyId = @CompanyId)
          AND (@ACCODE = '' OR H.ACCODE IN (SELECT AccCode FROM @TargetAccounts))
          AND ISNULL(H.TOTAMT, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           10. COMMISSION BOOK (COMMHEAD 'CB' NEGATIVE TOTAMT -> DRAMT)
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, ACTYPE,
            NARRATION, STATION
        )
        SELECT
            H.CompanyId, H.DOCNO, H.DOCDATE, 'CB',
            SUBSTRING(H.ACCODE, 1, 3), SUBSTRING(H.ACCODE, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.ACCODE END,
            ABS(ISNULL(H.TOTAMT, 0)), '3',
            H.VEHICLENO, H.STATION
        FROM COMMHEAD H
        WHERE (@CompanyId = 0 OR H.CompanyId = @CompanyId)
          AND (@ACCODE = '' OR H.ACCODE IN (SELECT AccCode FROM @TargetAccounts))
          AND ISNULL(H.TOTAMT, 0) < 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           11. CARTAGE IN SALES: LABOUR (COMMHEAD 'LB')
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE,
            NARRATION, STATION, INAME
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'LB',
            SUBSTRING(@ACCODE, 1, 3), SUBSTRING(@ACCODE, 4, 3),
            @ACCODE,
            ISNULL(H.LABOUR, 0), '2',
            H.VEHICLENO, H.STATION, H.TRANSPORTER
        FROM COMMHEAD H
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(@ACCODE) = (SELECT TOP 1 RTRIM(ACCODE) FROM ACPARA WHERE ACTYPE = 'E' AND (@CompanyId = 0 OR CompanyId = @CompanyId)))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.LABOUR, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           12. LOCAL AMOUNT (COMMHEAD 'MU')
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE,
            NARRATION, STATION
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'MU',
            SUBSTRING(@ACCODE, 1, 3), SUBSTRING(@ACCODE, 4, 3),
            @ACCODE,
            ISNULL(H.LOCALAMT, 0), '2',
            H.VEHICLENO, H.STATION
        FROM COMMHEAD H
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(@ACCODE) = (SELECT TOP 1 RTRIM(ACCODE) FROM ACPARA WHERE ACTYPE = 'U' AND (@CompanyId = 0 OR CompanyId = @CompanyId)))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.LOCALAMT, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           13. CARTAGE PAYABLE TO DRIVERS (ISSHEAD 'SL')
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE, NARRATION
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'SL',
            SUBSTRING(H.TRANSPORTER, 1, 3), SUBSTRING(H.TRANSPORTER, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.TRANSPORTER END,
            ISNULL(H.CARTAGE1, 0), 'L',
            'CARTAGE on Inv #: ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' ' + RTRIM(H.CARTTYPE) + ' Trip to Customer: ' + RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.TRANSPORTER) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.CARTAGE1, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE, NARRATION
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'SL',
            SUBSTRING(H.TRANSPORTER2, 1, 3), SUBSTRING(H.TRANSPORTER2, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.TRANSPORTER2 END,
            ISNULL(H.CARTAGE2, 0), 'L',
            'CARTAGE on Inv #: ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' ' + RTRIM(H.CARTTYPE2) + ' Trip to Customer: ' + RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.TRANSPORTER2) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.CARTAGE2, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE, NARRATION
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'SL',
            SUBSTRING(H.TRANSPORTER3, 1, 3), SUBSTRING(H.TRANSPORTER3, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.TRANSPORTER3 END,
            ISNULL(H.CARTAGE3, 0), 'L',
            'CARTAGE on Inv #: ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' ' + RTRIM(H.CARTTYPE3) + ' Trip to Customer: ' + RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.TRANSPORTER3) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.CARTAGE3, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           14. CHALLAN PARTY STATION DEBIT (CHALLANHEAD 'CL' DRAMT)
           ========================================================= */
        -- PExpAmt 1
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, ACTYPE,
            NARRATION, VEHICLENO, STATION, INAME, BILLTINO
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'CL',
            SUBSTRING(H.PARTYSTATIONCODE, 1, 3), SUBSTRING(H.PARTYSTATIONCODE, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.PARTYSTATIONCODE END,
            ISNULL(H.PEXPAMT, 0), 'A',
            H.VEHICLENO, H.VEHICLENO, 'Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''), G.NAME, H.PEXPBILTI
        FROM CHALLANHEAD H
        OUTER APPLY (
            SELECT TOP 1 G.NAME FROM GLCHART3 G WHERE H.PEXPCODE = RTRIM(G.AC1) + RTRIM(G.AC3) OR H.PEXPCODE = RTRIM(G.ACC)
        ) G
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.PARTYSTATIONCODE) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        -- PExpAmt 2
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, ACTYPE,
            NARRATION, VEHICLENO, STATION, INAME, BILLTINO
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'CL',
            SUBSTRING(H.PARTYSTATIONCODE, 1, 3), SUBSTRING(H.PARTYSTATIONCODE, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.PARTYSTATIONCODE END,
            ISNULL(H.PEXPAMT2, 0), 'A',
            H.VEHICLENO, H.VEHICLENO, 'Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''), G.NAME, H.PEXPBILTI2
        FROM CHALLANHEAD H
        OUTER APPLY (
            SELECT TOP 1 G.NAME FROM GLCHART3 G WHERE H.PEXPCODE2 = RTRIM(G.AC1) + RTRIM(G.AC3) OR H.PEXPCODE2 = RTRIM(G.ACC)
        ) G
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.PARTYSTATIONCODE) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT2, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        -- PExpAmt 3
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, DRAMT, ACTYPE,
            NARRATION, VEHICLENO, STATION, INAME, BILLTINO
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'CL',
            SUBSTRING(H.PARTYSTATIONCODE, 1, 3), SUBSTRING(H.PARTYSTATIONCODE, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.PARTYSTATIONCODE END,
            ISNULL(H.PEXPAMT3, 0), 'A',
            H.VEHICLENO, H.VEHICLENO, 'Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''), G.NAME, H.PEXPBILTI3
        FROM CHALLANHEAD H
        OUTER APPLY (
            SELECT TOP 1 G.NAME FROM GLCHART3 G WHERE H.PEXPCODE3 = RTRIM(G.AC1) + RTRIM(G.AC3) OR H.PEXPCODE3 = RTRIM(G.ACC)
        ) G
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.PARTYSTATIONCODE) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT3, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           15. CHALLAN PARTY EXPENSE LIABILITY CREDIT (CHALLANHEAD 'CL' CRAMT)
           ========================================================= */
        -- PExpCode 1
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE,
            NARRATION, VEHICLENO, STATION, BILLTINO
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'CL',
            SUBSTRING(H.PEXPCODE, 1, 3), SUBSTRING(H.PEXPCODE, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.PEXPCODE END,
            ISNULL(H.PEXPAMT, 0), 'L',
            H.VEHICLENO, H.VEHICLENO, H.STATION + ' Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''), H.PEXPBILTI
        FROM CHALLANHEAD H
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.PEXPCODE) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        -- PExpCode 2
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE,
            NARRATION, VEHICLENO, STATION, BILLTINO
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'CL',
            SUBSTRING(H.PEXPCODE2, 1, 3), SUBSTRING(H.PEXPCODE2, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.PEXPCODE2 END,
            ISNULL(H.PEXPAMT2, 0), 'L',
            H.VEHICLENO, H.VEHICLENO, H.STATION + ' Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''), H.PEXPBILTI2
        FROM CHALLANHEAD H
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.PEXPCODE2) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT2, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        -- PExpCode 3
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE,
            NARRATION, VEHICLENO, STATION, BILLTINO
        )
        SELECT
            CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END,
            H.DOCNO, H.DOCDATE, 'CL',
            SUBSTRING(H.PEXPCODE3, 1, 3), SUBSTRING(H.PEXPCODE3, 4, 3),
            CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.PEXPCODE3 END,
            ISNULL(H.PEXPAMT3, 0), 'L',
            H.VEHICLENO, H.VEHICLENO, H.STATION + ' Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''), H.PEXPBILTI3
        FROM CHALLANHEAD H
        WHERE (@CompanyId = 0 OR CASE WHEN H.CompanyId IN (1008, 1009) THEN 1007 ELSE H.CompanyId END = @CompanyId)
          AND (@ACCODE = '' OR RTRIM(H.PEXPCODE3) IN (SELECT AccCode FROM @TargetAccounts))
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT3, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           16. SALES TAX ON SALES INVOICES (ISSHEAD 'SL')
           ========================================================= */
        INSERT INTO ACCUMULATED
        (
            COCODE, VONO, VODATE, VOTYPE,
            AC1, AC3, ACC, CRAMT, ACTYPE, NARRATION
        )
        SELECT
            H.CompanyId, H.DOCNO, H.DOCDATE, 'SL',
            SUBSTRING(@ACCODE, 1, 3), SUBSTRING(@ACCODE, 4, 3),
            @ACCODE,
            ISNULL(H.STAXAMT, 0), '3',
            'SALES TAX ON SALES INVOICE # ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' @ ' + ISNULL(CONVERT(VARCHAR(50), H.STAX), '') + ' %'
        FROM ISSHEAD H
        WHERE (@CompanyId = 0 OR H.CompanyId = @CompanyId)
          AND H.INVDATE <= @TDATE
          AND ISNULL(H.STAXAMT, 0) > 0
          AND ISNULL(H.IsDeleted, 0) = 0;

        /* =========================================================
           17. WITHHOLDING TAX (Company 1006 / '01' / All)
           ========================================================= */
        IF @CompanyId = 1006 OR @CompanyId = 0
        BEGIN
            -- Income Tax - Customer Credit
            INSERT INTO ACCUMULATED
            (
                COCODE, VONO, VODATE, VOTYPE,
                AC1, AC3, ACC, CRAMT, ACTYPE, NARRATION
            )
            SELECT
                H.CompanyId, H.INVNO, H.WH_IT_REC, 'WT',
                SUBSTRING(H.CUSCODE, 1, 3), SUBSTRING(H.CUSCODE, 4, 3),
                CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.CUSCODE END,
                ISNULL(H.WH_IT_AMT, 0), '3',
                'WITH-HOLDING INCOME TAX ON SALES INVOICE # ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' @ ' + ISNULL(CONVERT(VARCHAR(50), H.WH_IT), '') + ' %'
            FROM ISSHEAD H
            WHERE H.CompanyId = 1006
              AND ISNULL(H.WH_IT_REC, '3000-12-31') <= @TDATE
              AND ISNULL(H.WH_IT_AMT, 0) > 0
              AND H.WH_IT_REC IS NOT NULL
              AND (@ACCODE = '' OR RTRIM(H.CUSCODE) IN (SELECT AccCode FROM @TargetAccounts))
              AND ISNULL(H.IsDeleted, 0) = 0;

            -- Income Tax Payable Debit
            INSERT INTO ACCUMULATED
            (
                COCODE, VONO, VODATE, VOTYPE,
                AC1, AC3, ACC, DRAMT, ACTYPE, NARRATION
            )
            SELECT
                H.CompanyId, H.INVNO, H.WH_IT_REC, 'WT',
                '051', '007',
                '051007',
                ISNULL(H.WH_IT_AMT, 0), '3',
                'W/H I.TAX ON INV.# ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' @ ' + ISNULL(CONVERT(VARCHAR(50), H.WH_IT), '') + ' % Cus. ' + LEFT(RTRIM(H.CUSNAME), 30)
            FROM ISSHEAD H
            WHERE H.CompanyId = 1006
              AND ISNULL(H.WH_IT_REC, '3000-12-31') <= @TDATE
              AND ISNULL(H.WH_IT_AMT, 0) > 0
              AND H.WH_IT_REC IS NOT NULL
              AND (@ACCODE = '' OR @ACCODE = '051007')
              AND ISNULL(H.IsDeleted, 0) = 0;

            -- Sales Tax - Customer Credit
            INSERT INTO ACCUMULATED
            (
                COCODE, VONO, VODATE, VOTYPE,
                AC1, AC3, ACC, CRAMT, ACTYPE, NARRATION
            )
            SELECT
                H.CompanyId, H.INVNO, H.WH_ST_REC, 'WT',
                SUBSTRING(H.CUSCODE, 1, 3), SUBSTRING(H.CUSCODE, 4, 3),
                CASE WHEN @ACCODE <> '' THEN @ACCODE ELSE H.CUSCODE END,
                ISNULL(H.WH_ST_AMT, 0), '3',
                'WITH-HOLDING SALES TAX ON SALES INVOICE # ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' @ ' + ISNULL(CONVERT(VARCHAR(50), H.WH_ST), '') + ' %'
            FROM ISSHEAD H
            WHERE H.CompanyId = 1006
              AND ISNULL(H.WH_ST_REC, '3000-12-31') <= @TDATE
              AND ISNULL(H.WH_ST_AMT, 0) > 0
              AND H.WH_ST_REC IS NOT NULL
              AND (@ACCODE = '' OR RTRIM(H.CUSCODE) IN (SELECT AccCode FROM @TargetAccounts))
              AND ISNULL(H.IsDeleted, 0) = 0;

            -- GST Payable Debit
            INSERT INTO ACCUMULATED
            (
                COCODE, VONO, VODATE, VOTYPE,
                AC1, AC3, ACC, DRAMT, ACTYPE, NARRATION
            )
            SELECT
                H.CompanyId, H.INVNO, H.WH_ST_REC, 'WT',
                '053', '001',
                '053001',
                ISNULL(H.WH_ST_AMT, 0), '3',
                'WITH-HOLDING SALES TAX ON SALES INVOICE # ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' @ ' + ISNULL(CONVERT(VARCHAR(50), H.WH_ST), '') + ' %'
            FROM ISSHEAD H
            WHERE H.CompanyId = 1006
              AND ISNULL(H.WH_ST_REC, '3000-12-31') <= @TDATE
              AND ISNULL(H.WH_ST_AMT, 0) > 0
              AND H.WH_ST_REC IS NOT NULL
              AND (@ACCODE = '' OR @ACCODE = '053001')
              AND ISNULL(H.IsDeleted, 0) = 0;
        END;

        /* =========================================================
           PREFIX INVOICE NUMBER IN NARRATION
           ========================================================= */
        UPDATE ACCUMULATED
        SET NARRATION = 'INV # ' + RTRIM(INVNO) + ' ' + RTRIM(NARRATION)
        WHERE INVNO IS NOT NULL AND INVNO <> '' AND INVNO <> '0';

        /* =========================================================
           OPENING BALANCE CALCULATION
           ========================================================= */
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