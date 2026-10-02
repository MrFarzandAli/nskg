-- dbo.PROCESSDETAIL_Station 1006,'2026-08-23','067003'
CREATE   PROCEDURE [dbo].[PROCESSDETAIL_Station]  
(
    @CompanyId int,
    @TDATE  DATE,
    @ACCODE VARCHAR(50)
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
        @FYEAR CHAR(1),
        @STAX1 CHAR(3),
        @STAX2 CHAR(3);

    BEGIN TRY

        /* =========================================================
           GET FINANCIAL YEAR TYPE
           Oracle: SELECT MAX(PERIOD.YEAR) FROM PERIOD WHERE COCODE = :COCODE
           ========================================================= */

        -- Assuming default 'H' for station procedure
        SELECT @FYEAR = 'H';

        SET @MDATE = @TDATE;
        SET @MON = RIGHT('0' + CAST(MONTH(@MDATE) AS VARCHAR(2)), 2);


        /* =========================================================
           CALCULATE START DATE
           ========================================================= */

        IF @FYEAR = 'H'
        BEGIN
            IF @MON BETWEEN '07' AND '12'
            BEGIN
                SET @SYEAR = RIGHT(CAST(YEAR(@MDATE) AS VARCHAR(4)), 2);
            END
            ELSE
            BEGIN
                SET @SYEAR =
                    RIGHT(
                        '0' +
                        CAST(
                            (YEAR(@MDATE) % 100) - 1
                            AS VARCHAR(2)
                        ),
                        2
                    );
            END;

            SET @SDATE =
                DATEFROMPARTS(
                    CASE
                        WHEN @MON BETWEEN '07' AND '12'
                            THEN YEAR(@MDATE)
                        ELSE YEAR(@MDATE) - 1
                    END,
                    7,
                    1
                );
        END
        ELSE
        BEGIN
            SET @SDATE = DATEFROMPARTS(YEAR(@MDATE), 1, 1);
        END;


        /* =========================================================
           CLEAR TEMP/ACCUMULATION TABLES
           ========================================================= */

        DELETE FROM ACCUMULATED;
        DELETE FROM ACCOPEN;


        /* =========================================================
           ACCOUNT OPENING
           ========================================================= */

        INSERT INTO ACCOPEN
        (
            AC1,
            AC3,
            ACTYPE,
            COCODE
        )
        SELECT
            AC1,
            AC3,
            ACTYPE,
            CompanyId
        FROM GLCHART3
        WHERE CompanyId = @CompanyId
          AND RTRIM(AC1) + RTRIM(AC3) = RTRIM(@ACCODE)
          AND CTYPE <> '4';


        /* =========================================================
           SALES / PURCHASE CONTROL ACCOUNTS FROM ACCOUNT PARAMETER TABLE
           ========================================================= */

        -- Update ISSHEAD with Sales account (ACTYPE = 'S')
        UPDATE ISSHEAD
        SET ACcCODE =
        (
            SELECT TOP 1 RTRIM(ACCODE)
            FROM ACPARA
            WHERE ACTYPE = 'S'
              AND CompanyId = @CompanyId
        );

        -- Update COMMHEAD with account (ACTYPE = 'D')
        UPDATE COMMHEAD
        SET ACCODE =
        (
            SELECT TOP 1 RTRIM(ACCODE)
            FROM ACPARA
            WHERE ACTYPE = 'D'
              AND CompanyId = @CompanyId
        );

        -- Update ISSDETAIL with Sales account (ACTYPE = 'S')
        UPDATE ISSDETAIL
        SET ACcCODE =
        (
            SELECT TOP 1 RTRIM(ACCODE)
            FROM ACPARA
            WHERE ACTYPE = 'S'
              AND CompanyId = @CompanyId
        );

        -- Update CommDetail with account (ACTYPE = 'D')
        UPDATE CommDetail
        SET ACCODE =
        (
            SELECT TOP 1 RTRIM(ACCODE)
            FROM ACPARA
            WHERE ACTYPE = 'D'
              AND CompanyId = @CompanyId
        );

       


        /* =========================================================
           DUE DATE
           Oracle: i.invdate + nvl(crdays,0)
           ========================================================= */

        UPDATE I
        SET I.DUEDATE =
        DATEADD
        (
            DAY,
            ISNULL(G.CRDAYS, 0),
            I.INVDATE
        )
        FROM ISSHEAD I
        INNER JOIN GLCHART3 G
            ON RTRIM(I.CUSCODE) =
               RTRIM(G.AC1) + RTRIM(G.AC3)
           AND G.ACTYPE <> 'S'
           AND G.CompanyId = I.CompanyId
        WHERE I.CompanyId = @CompanyId
          AND RTRIM(I.CUSCODE) = RTRIM(@ACCODE);


        /* =========================================================
           VOUCHER DETAIL (INVOICE = '0')
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            DRAMT,
            CRAMT,
            NARRATION,
            CHQNO,
            CHQDATE,
            ACTYPE,
            INVNO,
            BILLTINO,
            BILNO
        )
        SELECT
            h.CompanyId,
            d.VONO,
            d.VODATE,
            d.VOTYPE,
            SUBSTRING(HACC, 1, 3),
            SUBSTRING(HACC, 4, 3),
            CRAMT,
            DRAMT,
            RTRIM(d.NARRATION),
            CHQNO,
            CHQDATE,
            '2',
            d.INVNO,
            BILLTINO,
            BILNO
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE h.CompanyId = @CompanyId
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') = '0'
          AND ISNULL(HACC, '999999') = @ACCODE
          AND ACC <> ISNULL(HACC, '999999')
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           INVOICE OFFSET (INVOICE != '0')
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            DRAMT,
            CRAMT,
            NARRATION,
            CHQNO,
            CHQDATE,
            ACTYPE
        )
        SELECT
            h.CompanyId,
            d.VONO,
            d.VODATE,
            d.VOTYPE,
            SUBSTRING(HACC, 1, 3),
            SUBSTRING(HACC, 4, 3),
            SUM(ISNULL(CRAMT, 0)),
            SUM(ISNULL(DRAMT, 0)),
            RTRIM(d.NARRATION),
            CHQNO,
            CHQDATE,
            '2'
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE h.CompanyId = @CompanyId
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') <> '0'
          AND ISNULL(HACC, '999999') = @ACCODE
          AND ACC <> ISNULL(HACC, '999999')
          and ISNULL(h.IsDeleted, 0) = 0
        GROUP BY
            h.CompanyId,
            d.VONO,
            d.VODATE,
            d.VOTYPE,
            SUBSTRING(HACC, 1, 3),
            SUBSTRING(HACC, 4, 3),
            RTRIM(d.NARRATION),
            CHQNO,
            CHQDATE;


        /* =========================================================
           VOUCHER DETAIL - TRANSCODE (INVOICE = '0', TRANSCODE = ACCODE)
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            CRAMT,
            DRAMT,
            NARRATION,
            CHQNO,
            CHQDATE,
            ACTYPE,
            INVNO,
            BILLTINO,
            BILNO,
            VEHICLENO,
            INAME
        )
        SELECT
            h.CompanyId,
            d.VONO,
            d.VODATE,
            d.VOTYPE,
            SUBSTRING(ISNULL(TRANSCODE, '0'), 1, 3),
            SUBSTRING(ISNULL(TRANSCODE, '0'), 4, 6),
            CRAMT,
            DRAMT,
            RTRIM(d.NARRATION),
            CHQNO,
            CHQDATE,
            '2',
            d.INVNO,
            BILLTINO,
            BILNO,
            VEHICLENO,
            RTRIM(NAME)
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE h.CompanyId = @CompanyId
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') = '0'
          AND ISNULL(HACC, '999999') <> @ACCODE
          AND RTRIM(TRANSCODE) = RTRIM(@ACCODE)
          AND RTRIM(d.AC1) + RTRIM(d.AC3) <> ISNULL(HACC, '999999')
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           OTHER VOUCHER DETAILS (INVOICE = '0', HACC != ACCODE)
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            DRAMT,
            CRAMT,
            NARRATION,
            CHQNO,
            CHQDATE,
            ACTYPE,
            INVNO,
            BILLTINO,
            BILNO,
            VEHICLENO,
            INAME
        )
        SELECT
            h.CompanyId,
            d.VONO,
            d.VODATE,
            d.VOTYPE,
            d.AC1,
            d.AC3,
            DRAMT,
            CRAMT,
            RTRIM(d.NARRATION),
            CHQNO,
            CHQDATE,
            '2',
            d.INVNO,
            BILLTINO,
            BILNO,
            VEHICLENO,
            RTRIM(NAME)
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE h.CompanyId = @CompanyId
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') = '0'
          AND ISNULL(HACC, '999999') <> @ACCODE
          AND RTRIM(d.AC1) + RTRIM(d.AC3) = RTRIM(@ACCODE)
          AND RTRIM(d.AC1) + RTRIM(d.AC3) <> ISNULL(HACC, '999999')
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           INVOICE OFFSET - OTHER ACCOUNT (INVOICE != '0')
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            DRAMT,
            CRAMT,
            NARRATION,
            CHQNO,
            CHQDATE,
            ACTYPE
        )
        SELECT
            h.CompanyId,
            d.VONO,
            d.VODATE,
            d.VOTYPE,
            d.AC1,
            d.AC3,
            SUM(ISNULL(DRAMT, 0)),
            SUM(ISNULL(CRAMT, 0)),
            RTRIM(d.NARRATION),
            CHQNO,
            CHQDATE,
            '2'
        FROM VODET d
        JOIN VOHEAD h ON h.Id = d.VOHEADId
        WHERE h.CompanyId = @CompanyId
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') <> '0'
          AND ISNULL(HACC, '999999') <> @ACCODE
          AND RTRIM(d.AC1) + RTRIM(d.AC3) = RTRIM(@ACCODE)
          AND RTRIM(d.AC1) + RTRIM(d.AC3) <> ISNULL(HACC, '999999')
          and ISNULL(h.IsDeleted, 0) = 0
        GROUP BY
            h.CompanyId,
            d.VONO,
            d.VODATE,
            d.VOTYPE,
            d.AC1,
            d.AC3,
            RTRIM(d.NARRATION),
            CHQNO,
            CHQDATE;


        /* =========================================================
           ITEM WISE SALES
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            DRAMT,
            ACTYPE,
            NARRATION,
            INAME,
            QTY,
            BILLTINO,
            BILNO
        )
        SELECT
            H.CompanyId,
            H.DOCNO,
            H.DOCDATE,
            'BL',
            SUBSTRING(H.CUSCODE, 1, 3),
            SUBSTRING(H.CUSCODE, 4, 3),
            ISNULL(H.NETAMT, 0),
            '3',
            'Vehicle:' + H.VEHICLENO,
            RTRIM(D.INAME),
            D.QTY,
            H.BILLTINO,
            H.BILNO
        FROM ISSDETAIL D
        INNER JOIN ISSHEAD H
            ON RTRIM(D.DOCNO) = RTRIM(H.DOCNO)
           AND D.CompanyId = H.CompanyId
        WHERE H.CompanyId = @CompanyId
          AND RTRIM(H.CUSCODE) = RTRIM(@ACCODE)
          AND RTRIM(D.CUSCODE) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND D.CompanyId = @CompanyId
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           STATION (COMMHEAD) - POSITIVE AMOUNT
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            BILLTINO,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            DRAMT,
            ACTYPE,
            NARRATION,
            VEHICLENO,
            INAME,
            NCRAMT,
            NDRAMT,
            AMOUNT,
            DELIVERYAMT,
            DELIVERYAMT2
        )
        SELECT
            H.CompanyId,
            H.CHALNO,
            H.DOCNO,
            H.DOCDATE,
            'ST',
            SUBSTRING(H.STATIONCODE, 1, 3),
            SUBSTRING(H.STATIONCODE, 4, 3),
            ISNULL(H.TOTAMT, 0),
            '3',
            H.NARRATION,
            H.VEHICLENO,
            H.STATION,
            H.TOTNET,
            H.LOCALAMT,
            H.DELIVERYAMT,
            H.DELIVERYAMT1,
            H.DELIVERYAMT2
        FROM COMMHEAD H
        WHERE H.CompanyId = @CompanyId
          AND RTRIM(H.STATIONCODE) = RTRIM(@ACCODE)
          AND ISNULL(H.STATIONAMT, 0) > 0
          AND LEFT(CAST(ISNULL(H.STATIONAMT, 0) AS VARCHAR(50)), 1) <> '-'
          AND H.DOCDATE <= @TDATE
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           STATION (COMMHEAD) - NEGATIVE AMOUNT (Reversal)
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            BILLTINO,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            CRAMT,
            ACTYPE,
            NARRATION,
            VEHICLENO,
            INAME,
            NCRAMT,
            NDRAMT,
            AMOUNT,
            DELIVERYAMT,
            DELIVERYAMT2
        )
        SELECT
            H.CompanyId,
            H.CHALNO,
            H.DOCNO,
            H.DOCDATE,
            'ST',
            SUBSTRING(H.STATIONCODE, 1, 3),
            SUBSTRING(H.STATIONCODE, 4, 3),
            ABS(ISNULL(H.STATIONAMT, 0)),
            '3',
            H.NARRATION,
            H.VEHICLENO,
            H.STATION,
            H.TOTNET,
            H.LOCALAMT,
            H.DELIVERYAMT,
            H.DELIVERYAMT1,
            H.DELIVERYAMT2
        FROM COMMHEAD H
        WHERE H.CompanyId = @CompanyId
          AND RTRIM(H.STATIONCODE) = RTRIM(@ACCODE)
          AND LEFT(CAST(ISNULL(H.STATIONAMT, 0) AS VARCHAR(50)), 1) = '-'
          AND H.DOCDATE <= @TDATE
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           SALES ACCOUNT
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            CRAMT,
            ACTYPE,
            NARRATION,
            RATE,
            QTY
        )
        SELECT
            H.CompanyId,
            H.DOCNO,
            H.DOCDATE,
            'SL',
            SUBSTRING(H.ACcCODE, 1, 3),
            SUBSTRING(H.ACcCODE, 4, 3),
            ISNULL(D.AMOUNT, 0) + ISNULL(D.STAXAMT, 0),
            '3',
            'SALES Qty: ',
            D.RATE,
            D.QTY
        FROM ISSHEAD H
        INNER JOIN ISSDETAIL D
            ON RTRIM(D.DOCNO) = RTRIM(H.DOCNO)
           AND D.CompanyId = H.CompanyId
        WHERE H.CompanyId = @CompanyId
          AND H.ACcCODE = @ACCODE
          AND D.CompanyId = @CompanyId
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           CARTAGE IN SALES (Debit to Expense)
           ========================================================= */

        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            DRAMT,
            ACTYPE,
            NARRATION
        )
        SELECT
            CASE
                WHEN H.CompanyId IN (1008,1009) THEN 1007
                ELSE H.CompanyId
            END,
            H.DOCNO,
            H.DOCDATE,
            'CT',
            SUBSTRING(@ACCODE, 1, 3),
            SUBSTRING(@ACCODE, 4, 3),
            ISNULL(H.CARTAGE1, 0) + ISNULL(H.CARTAGE2, 0) + ISNULL(H.CARTAGE3, 0),
            '2',
            'CARTAGE on Inv #: ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' Customer: ' + RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE
            CASE
                WHEN H.CompanyId IN (1008,1009) THEN 1007
                ELSE H.CompanyId
            END = @CompanyId
          AND RTRIM(@ACCODE) =
              (
                  SELECT TOP 1 RTRIM(ACCODE)
                  FROM ACPARA
                  WHERE ACTYPE = 'E'
                    AND CompanyId = @CompanyId
              )
          AND H.DOCDATE <= @TDATE
          AND (ISNULL(H.CARTAGE1, 0) + ISNULL(H.CARTAGE2, 0) + ISNULL(H.CARTAGE3, 0)) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           CARTAGE PAYABLE TO DRIVERS (Credit to Transporter)
           ========================================================= */

        -- Transporter 1
        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            CRAMT,
            ACTYPE,
            NARRATION
        )
        SELECT
            CASE
                WHEN H.CompanyId IN (1008,1009) THEN 1007
                ELSE H.CompanyId
            END,
            H.DOCNO,
            H.DOCDATE,
            'SL',
            SUBSTRING(H.TRANSPORTER, 1, 3),
            SUBSTRING(H.TRANSPORTER, 4, 3),
            ISNULL(H.CARTAGE1, 0),
            'L',
            'CARTAGE on Inv #: ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' ' + RTRIM(H.CARTTYPE) + ' Trip to Customer: ' + RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE
            CASE
                WHEN H.CompanyId IN (1008,1009) THEN 1007
                ELSE H.CompanyId
            END = @CompanyId
          AND RTRIM(H.TRANSPORTER) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.CARTAGE1, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;

        -- Transporter 2
        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            CRAMT,
            ACTYPE,
            NARRATION
        )
        SELECT
            CASE
                WHEN H.CompanyId IN (1008,1009) THEN 1007
                ELSE H.CompanyId
            END,
            H.DOCNO,
            H.DOCDATE,
            'SL',
            SUBSTRING(H.TRANSPORTER2, 1, 3),
            SUBSTRING(H.TRANSPORTER2, 4, 3),
            ISNULL(H.CARTAGE2, 0),
            'L',
            'CARTAGE on Inv #: ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' ' + RTRIM(H.CARTTYPE2) + ' Trip to Customer: ' + RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE
            CASE
                WHEN H.CompanyId IN (1008,1009) THEN 1007
                ELSE H.CompanyId
            END = @CompanyId
          AND RTRIM(H.TRANSPORTER2) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.CARTAGE2, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;

        -- Transporter 3
        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            CRAMT,
            ACTYPE,
            NARRATION
        )
        SELECT
            CASE
                WHEN H.CompanyId IN (1008,1009) THEN 1007
                ELSE H.CompanyId
            END,
            H.DOCNO,
            H.DOCDATE,
            'SL',
            SUBSTRING(H.TRANSPORTER3, 1, 3),
            SUBSTRING(H.TRANSPORTER3, 4, 3),
            ISNULL(H.CARTAGE3, 0),
            'L',
            'CARTAGE on Inv #: ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' ' + RTRIM(H.CARTTYPE3) + ' Trip to Customer: ' + RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE
            CASE
                WHEN H.CompanyId IN (1008,1009) THEN 1007
                ELSE H.CompanyId
            END = @CompanyId
          AND RTRIM(H.TRANSPORTER3) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.CARTAGE3, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


      

        /* =========================================================
           COMPANY 01 ONLY - WITHHOLDING TAX
           ========================================================= */

        IF @CompanyId = 1006
        BEGIN

            -- Income Tax - Customer Credit
            INSERT INTO ACCUMULATED
            (
                COCODE, VONO, VODATE, VOTYPE,
                AC1, AC3, CRAMT, ACTYPE, NARRATION
            )
            SELECT
                H.CompanyId,
                H.INVNO,
                H.WH_IT_REC,
                'WT',
                SUBSTRING(H.CUSCODE, 1, 3),
                SUBSTRING(H.CUSCODE, 4, 3),
                ISNULL(H.WH_IT_AMT, 0),
                '3',
                'WITH-HOLDING INCOME TAX ON SALES INVOICE # ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' @ ' + ISNULL(CONVERT(VARCHAR(50),H.WH_IT), '') + ' %'
            FROM ISSHEAD H
            WHERE H.CompanyId = @CompanyId
              AND ISNULL(H.WH_IT_REC, '3000-12-31') <= @TDATE
              AND ISNULL(H.WH_IT_AMT, 0) > 0
              AND H.WH_IT_REC IS NOT NULL
              AND RTRIM(H.CUSCODE) = RTRIM(@ACCODE)
          and ISNULL(h.IsDeleted, 0) = 0;

            -- Income Tax Payable Debit
            INSERT INTO ACCUMULATED
            (
                COCODE, VONO, VODATE, VOTYPE,
                AC1, AC3, DRAMT, ACTYPE, NARRATION
            )
            SELECT
                H.CompanyId,
                H.INVNO,
                H.WH_IT_REC,
                'WT',
                '051',
                '007',
                ISNULL(H.WH_IT_AMT, 0),
                '3',
                'W/H I.TAX ON INV.# ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' @ ' + ISNULL(CONVERT(VARCHAR(50),H.WH_IT), '') + ' % Cus. ' + LEFT(RTRIM(H.CUSNAME), 30)
            FROM ISSHEAD H
            WHERE H.CompanyId = @CompanyId
              AND ISNULL(H.WH_IT_REC, '3000-12-31') <= @TDATE
              AND ISNULL(H.WH_IT_AMT, 0) > 0
              AND H.WH_IT_REC IS NOT NULL
              AND @ACCODE = '051007'
          and ISNULL(h.IsDeleted, 0) = 0;

            -- Sales Tax - Customer Credit
            INSERT INTO ACCUMULATED
            (
                COCODE, VONO, VODATE, VOTYPE,
                AC1, AC3, CRAMT, ACTYPE, NARRATION
            )
            SELECT
                H.CompanyId,
                H.INVNO,
                H.WH_ST_REC,
                'WT',
                SUBSTRING(H.CUSCODE, 1, 3),
                SUBSTRING(H.CUSCODE, 4, 3),
                ISNULL(H.WH_ST_AMT, 0),
                '3',
                'WITH-HOLDING SALES TAX ON SALES INVOICE # ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' @ ' + ISNULL(CONVERT(VARCHAR(50),H.WH_ST), '') + ' %'
            FROM ISSHEAD H
            WHERE H.CompanyId = @CompanyId
              AND ISNULL(H.WH_ST_REC, '3000-12-31') <= @TDATE
              AND ISNULL(H.WH_ST_AMT, 0) > 0
              AND H.WH_ST_REC IS NOT NULL
              AND RTRIM(H.CUSCODE) = RTRIM(@ACCODE)
          and ISNULL(h.IsDeleted, 0) = 0;

            -- GST Payable Debit
            INSERT INTO ACCUMULATED
            (
                COCODE, VONO, VODATE, VOTYPE,
                AC1, AC3, DRAMT, ACTYPE, NARRATION
            )
            SELECT
                H.CompanyId,
                H.INVNO,
                H.WH_ST_REC,
                'WT',
                '053',
                '001',
                ISNULL(H.WH_ST_AMT, 0),
                '3',
                'WITH-HOLDING SALES TAX ON SALES INVOICE # ' + ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') + ' @ ' + ISNULL(CONVERT(VARCHAR(50),H.WH_ST), '') + ' %'
            FROM ISSHEAD H
            WHERE H.CompanyId = @CompanyId
              AND ISNULL(H.WH_ST_REC, '3000-12-31') <= @TDATE
              AND ISNULL(H.WH_ST_AMT, 0) > 0
              AND H.WH_ST_REC IS NOT NULL
              AND @ACCODE = '053001'
          and ISNULL(h.IsDeleted, 0) = 0;

        END;


     

        /* =========================================================
           PREFIX INVOICE NUMBER IN NARRATION
           ========================================================= */

        UPDATE ACCUMULATED
        SET NARRATION =
            'INV # ' +
            RTRIM(INVNO) +
            ' ' +
            RTRIM(NARRATION)
        WHERE INVNO IS NOT NULL;


        /* =========================================================
           OPENING BALANCE
           ========================================================= */

        UPDATE AO
        SET OPENING =
        (
            SELECT
                SUM(
                    ISNULL(A.DRAMT, 0) -
                    ISNULL(A.CRAMT, 0)
                )
            FROM ACCUMULATED A
            WHERE A.VODATE < @SDATE
        )
        FROM ACCOPEN AO;


        /* =========================================================
           COMMIT
           ========================================================= */

        -- Note: The Oracle version runs a report based on @ACCODE.
        -- In SQL Server, you would handle this differently.
        -- The report execution logic has been removed as it's application-specific.

        RETURN 0;

    END TRY
    BEGIN CATCH
        THROW;
    END CATCH;

END;

