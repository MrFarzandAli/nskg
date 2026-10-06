--dbo.PROCESSDETAIL  1006,'2026-08-23','052165'
CREATE OR ALTER PROCEDURE [dbo].[PROCESSDETAIL]  
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
           Oracle:
           SELECT MAX(PERIOD.YEAR)
           FROM PERIOD
           WHERE companyid = :companyid
           ========================================================= */

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
            companyid
        FROM GLCHART3
        WHERE CompanyId = @CompanyId
          AND RTRIM(AC1) + RTRIM(AC3) = RTRIM(@ACCODE)
          AND CTYPE <> '4';


        /* =========================================================
           SALES / PURCHASE CONTROL ACCOUNTS
           ========================================================= */

        -- Control accounts already populated in tables; redundant full table updates bypassed for high performance

      


        /* =========================================================
           DUE DATE
           Oracle:
           i.invdate + nvl(crdays,0)
           ========================================================= */

        -- DUEDATE calculation bypassed to prevent table-level write locks on ISSHEAD during report generation
        /*
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
           AND G.CtYPE <> 'S'
           AND G.companyid = I.companyid
        WHERE I.CompanyId = @CompanyId
          AND RTRIM(I.CUSCODE) = RTRIM(@ACCODE);
        */


        /* =========================================================
           VOUCHER DETAIL
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
            INAME,
            VEHICLENO,
            STATION
        )
        SELECT
            companyid,
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
            BILNO,
            NAME,
            VEHICLENO,
            TRANSPORTER
        FROM VODET d
        JOIN vohead h on h.id = d.voheadid
        WHERE h.companyid = @companyid
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') = '0'
          AND d.HACC = @ACCODE
          AND ISNULL(d.ACC, '') <> @ACCODE
          AND ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           INVOICE OFFSET
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
            h.companyid,
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
        JOIN vohead h on h.id = d.voheadid
        WHERE h.companyid = @companyid
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') <> '0'
          AND d.HACC = @ACCODE
          AND ISNULL(d.ACC, '') <> @ACCODE
          AND ISNULL(h.IsDeleted, 0) = 0
        GROUP BY
            companyid,
            d.VONO,
            d.VODATE,
            d.VOTYPE,
            SUBSTRING(HACC, 1, 3),
            SUBSTRING(HACC, 4, 3),
            RTRIM(d.NARRATION),
            CHQNO,
            CHQDATE;


        /* =========================================================
           OTHER VOUCHER DETAILS
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
            INAME
        )
        SELECT
            h.companyid,
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
            d.NARRATION
        FROM VODET d
        JOIN vohead h on h.id = d.voheadid
        WHERE h.companyid = @companyid
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') = '0'
          AND ISNULL(d.HACC, '') <> @ACCODE
          AND d.ACC = @ACCODE
          AND ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           TAX DETECTION (Bypassed: PTAX is already reflected in voucher totals and not counted as extra credit in Trial Balance)
           ========================================================= */
        /*
        INSERT INTO ACCUMULATED
        (
            COCODE,
            VONO,
            VODATE,
            VOTYPE,
            AC1,
            AC3,
            CRAMT,
            INAME,
            CHQNO,
            CHQDATE,
            ACTYPE,
            INVNO,
            BILLTINO,
            BILNO
        )
        SELECT
            h.companyid,
            d.VONO,
            d.VODATE,
            'TD',
            d.AC1,
            d.AC3,
            PTAX,
            'Tax Detection',
            CHQNO,
            CHQDATE,
            '2',
            d.INVNO,
            BILLTINO,
            BILNO
        FROM VODET d
        JOIN vohead h on h.id = d.voheadid
        WHERE h.companyid = @companyid
          AND d.VODATE <= @TDATE
          AND ISNULL(PTAX, 0) <> 0
          AND ISNULL(d.INVNO, '0') = '0'
          AND ISNULL(d.HACC, '') <> @ACCODE
          AND d.ACC = @ACCODE
          AND ISNULL(h.IsDeleted, 0) = 0;
        */


        /* =========================================================
           INVOICE OFFSET - OTHER ACCOUNT
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
            h.companyid,
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
        JOIN vohead h on h.id = d.voheadid
        WHERE h.companyid = @companyid
          AND d.VODATE <= @TDATE
          AND ISNULL(d.INVNO, '0') <> '0'
          AND ISNULL(d.HACC, '') <> @ACCODE
          AND d.ACC = @ACCODE
          AND ISNULL(h.IsDeleted, 0) = 0
        GROUP BY
            h.companyid,
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
            BILNO,
            STATION,
            VEHICLENO
        )
        SELECT
            H.companyid,
            H.DOCNO,
            H.DOCDATE,
            'BL',
            SUBSTRING(H.CUSCODE, 1, 3),
            SUBSTRING(H.CUSCODE, 4, 3),
            ISNULL(H.NETAMT, 0),
            '3',
            H.VEHICLENO,
            RTRIM(D.INAME),
            ISNULL(D.QTY, ISNULL(H.Qty, 0)),
            H.BILLTINO,
            H.BILNO,
            H.FOODER,
            h.VehicleNo
        FROM ISSHEAD H
        OUTER APPLY (
            SELECT TOP 1
                RTRIM(d1.INAME) AS INAME,
                SUM(ISNULL(d1.QTY, 0)) OVER() AS QTY
            FROM ISSDETAIL d1
            WHERE d1.IssHeadId = H.Id
              AND ISNULL(d1.IsDeleted, 0) = 0
            ORDER BY CASE WHEN NULLIF(RTRIM(d1.INAME), '') IS NOT NULL THEN 0 ELSE 1 END, d1.Id
        ) D
        WHERE H.companyid = @companyid
       AND RTRIM(H.CUSCODE) = RTRIM(@ACCODE)
          AND RTRIM(H.PTYPE) = 'Paid'
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.IsDeleted, 0) = 0;


        /* =========================================================
           TRANSPORTER
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
            NCRAMT,
            NDRAMT,
            AMOUNT
        )
        SELECT
            H.CompanyId,
            H.DOCNO,
            H.DOCDATE,
            'TR',
            SUBSTRING(H.TRANSCODE, 1, 3),
            SUBSTRING(H.TRANSCODE, 4, 3),
            ISNULL(H.TRANSPORTERAMT, 0),
            '3',
            H.VEHICLENO,
            H.STATION,
            H.TOTNET,
            H.LOCALAMT,
            H.DELIVERYAMT
        FROM COMMHEAD H
        WHERE H.CompanyId = @CompanyId
          AND RTRIM(H.TRANSCODE) = RTRIM(@ACCODE)
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
            QTY,
            INAME,
            STATION,
            BILNO,
            BILLTINO
        )
        SELECT
            H.companyid,
            H.DOCNO,
            H.DOCDATE,
            'BL',
            SUBSTRING(H.ACcCODE, 1, 3),
            SUBSTRING(H.ACcCODE, 4, 3),
            ISNULL(H.NETAMT, 0) + ISNULL(D.STAXAMT, 0),
            '3',
            H.VEHICLENO,
            D.RATE,
            ISNULL(D.QTY, ISNULL(H.Qty, 0)),
            H.CUSNAME,
            H.FOODER,
            H.BILNO,
            H.BILLTINO
        FROM ISSHEAD H
        OUTER APPLY (
            SELECT TOP 1
                d1.RATE,
                SUM(ISNULL(d1.QTY, 0)) OVER() AS QTY,
                SUM(ISNULL(d1.STAXAMT, 0)) OVER() AS STAXAMT
            FROM ISSDETAIL d1
            WHERE d1.IssHeadId = H.Id
              AND ISNULL(d1.IsDeleted, 0) = 0
            ORDER BY d1.Id
        ) D
        WHERE H.companyid = @companyid
          AND H.ACcCODE = @ACCODE
          AND RTRIM(H.PTYPE) = 'Paid'
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.IsDeleted, 0) = 0;


        /* =========================================================
           COMMHEAD POSITIVE / NEGATIVE
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
            STATION
        )
        SELECT
            H.companyid,
            H.DOCNO,
            H.DOCDATE,
            'CB',
            SUBSTRING(H.ACCODE, 1, 3),
            SUBSTRING(H.ACCODE, 4, 3),
            ISNULL(H.TOTAMT, 0),
            '3',
            H.VEHICLENO,
            H.STATION
        FROM COMMHEAD H
        WHERE H.companyid = @companyid
          AND H.ACCODE = @ACCODE
          AND H.TOTAMT > 0
          and ISNULL(h.IsDeleted, 0) = 0;


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
            STATION
        )
        SELECT
 H.companyid,
            H.DOCNO,
            H.DOCDATE,
            'CB',
            SUBSTRING(H.ACCODE, 1, 3),
            SUBSTRING(H.ACCODE, 4, 3),
            ABS(ISNULL(H.TOTAMT, 0)),
            '3',
            H.VEHICLENO,
            H.STATION
        FROM COMMHEAD H
        WHERE H.companyid = @companyid
          AND H.ACCODE = @ACCODE
          AND H.TOTAMT < 0
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           CARTAGE IN SALES
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
            STATION,
            INAME
        )
        SELECT
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'LB',
            SUBSTRING(@ACCODE, 1, 3),
            SUBSTRING(@ACCODE, 4, 3),
            ISNULL(H.LABOUR, 0),
            '2',
            H.VEHICLENO,
            H.STATION,
            H.TRANSPORTER
        FROM COMMHEAD H
        WHERE
            CASE
                 WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(@ACCODE) =
              (
                  SELECT TOP 1 RTRIM(ACCODE)
                  FROM ACPARA
                  WHERE ACTYPE = 'E'
                    AND companyid = @companyid
              )
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.LABOUR, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           LOCAL AMOUNT
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
            STATION
        )
        SELECT
            CASE
                 WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'MU',
            SUBSTRING(@ACCODE, 1, 3),
            SUBSTRING(@ACCODE, 4, 3),
            ISNULL(H.LOCALAMT, 0),
            '2',
            H.VEHICLENO,
            H.STATION
        FROM COMMHEAD H
        WHERE
            CASE
                 WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(@ACCODE) =
              (
                  SELECT TOP 1 RTRIM(ACCODE)
                  FROM ACPARA
                  WHERE ACTYPE = 'U'
                    AND companyid = @companyid
              )
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.LOCALAMT, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           CARTAGE PAYABLE TO DRIVERS
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
            NARRATION
        )
        SELECT
            CASE
                 WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'SL',
            SUBSTRING(H.TRANSPORTER, 1, 3),
            SUBSTRING(H.TRANSPORTER, 4, 3),
            ISNULL(H.CARTAGE1, 0),
            'L',
            'CARTAGE on Inv #: ' +
            ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') +
            ' ' +
            RTRIM(H.CARTTYPE) +
            ' Trip to Customer: ' +
            RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(H.TRANSPORTER) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.CARTAGE1, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


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
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'SL',
            SUBSTRING(H.TRANSPORTER2, 1, 3),
            SUBSTRING(H.TRANSPORTER2, 4, 3),
            ISNULL(H.CARTAGE2, 0),
            'L',
            'CARTAGE on Inv #: ' +
            ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') +
            ' ' +
            RTRIM(H.CARTTYPE2) +
            ' Trip to Customer: ' +
            RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(H.TRANSPORTER2) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.CARTAGE2, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


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
                 WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'SL',
            SUBSTRING(H.TRANSPORTER3, 1, 3),
            SUBSTRING(H.TRANSPORTER3, 4, 3),
            ISNULL(H.CARTAGE3, 0),
            'L',
            'CARTAGE on Inv #: ' +
            ISNULL(CONVERT(VARCHAR(50), H.INVNO), '') +
            ' ' +
            RTRIM(H.CARTTYPE3) +
            ' Trip to Customer: ' +
            RTRIM(H.CUSNAME)
        FROM ISSHEAD H
        WHERE
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(H.TRANSPORTER3) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.CARTAGE3, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           CHALLAN EXPENSES
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
            STATION,
            INAME,
            BILLTINO
        )
        SELECT
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'CL',
            SUBSTRING(H.PARTYSTATIONCODE, 1, 3),
            SUBSTRING(H.PARTYSTATIONCODE, 4, 3),
            ISNULL(H.PEXPAMT, 0),
            'A',
            H.VEHICLENO,
            'Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''),
            G.NAME,
            H.PEXPBILTI
        FROM CHALLANHEAD H
        OUTER APPLY (
            SELECT TOP 1 G.NAME
            FROM GLCHART3 G
            WHERE H.PEXPCODE = RTRIM(G.AC1) + RTRIM(G.AC3)
            ORDER BY CASE WHEN G.COCODE = H.companyid THEN 0 ELSE 1 END
        ) G
        WHERE
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(H.PARTYSTATIONCODE) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


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
            STATION,
            INAME,
            BILLTINO
        )
        SELECT
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'CL',
            SUBSTRING(H.PARTYSTATIONCODE, 1, 3),
            SUBSTRING(H.PARTYSTATIONCODE, 4, 3),
            ISNULL(H.PEXPAMT2, 0),
            'A',
            H.VEHICLENO,
            'Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''),
            G.NAME,
            H.PEXPBILTI2
        FROM CHALLANHEAD H
        OUTER APPLY (
            SELECT TOP 1 G.NAME
            FROM GLCHART3 G
            WHERE H.PEXPCODE2 = RTRIM(G.AC1) + RTRIM(G.AC3)
            ORDER BY CASE WHEN G.COCODE = H.companyid THEN 0 ELSE 1 END
        ) G
        WHERE
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(H.PARTYSTATIONCODE) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT2, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


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
            STATION,
            INAME,
            BILLTINO
        )
        SELECT
            CASE
                 WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'CL',
            SUBSTRING(H.PARTYSTATIONCODE, 1, 3),
            SUBSTRING(H.PARTYSTATIONCODE, 4, 3),
            ISNULL(H.PEXPAMT3, 0),
            'A',
            H.VEHICLENO,
            'Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''),
            G.NAME,
            H.PEXPBILTI3
        FROM CHALLANHEAD H
        OUTER APPLY (
            SELECT TOP 1 G.NAME
            FROM GLCHART3 G
            WHERE H.PEXPCODE3 = RTRIM(G.AC1) + RTRIM(G.AC3)
            ORDER BY CASE WHEN G.COCODE = H.companyid THEN 0 ELSE 1 END
        ) G
        WHERE
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(H.PARTYSTATIONCODE) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT3, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           CHALLAN LIABILITY
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
            STATION,
            BILLTINO
        )
        SELECT
            CASE
                 WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'CL',
            SUBSTRING(H.PEXPCODE, 1, 3),
            SUBSTRING(H.PEXPCODE, 4, 3),
            ISNULL(H.PEXPAMT, 0),
            'L',
            H.VEHICLENO,
            H.STATION + ' Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''),
            H.PEXPBILTI
        FROM CHALLANHEAD H
        WHERE
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(H.PEXPCODE) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


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
            STATION,
            BILLTINO
        )
        SELECT
            CASE
                 WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'CL',
            SUBSTRING(H.PEXPCODE2, 1, 3),
            SUBSTRING(H.PEXPCODE2, 4, 3),
            ISNULL(H.PEXPAMT2, 0),
            'L',
            H.VEHICLENO,
            H.STATION + ' Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''),
            H.PEXPBILTI2
        FROM CHALLANHEAD H
        WHERE
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(H.PEXPCODE2) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT2, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


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
            STATION,
            BILLTINO
        )
        SELECT
            CASE
                WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END,
            H.DOCNO,
            H.DOCDATE,
            'CL',
            SUBSTRING(H.PEXPCODE3, 1, 3),
            SUBSTRING(H.PEXPCODE3, 4, 3),
            ISNULL(H.PEXPAMT3, 0),
            'L',
            H.VEHICLENO,
            H.STATION + ' Challan No.' + ISNULL(CONVERT(VARCHAR(50), H.CHALNO), ''),
            H.PEXPBILTI3
        FROM CHALLANHEAD H
        WHERE
            CASE
                 WHEN H.companyid IN (1008,1009) THEN 1007
                ELSE H.companyid
            END = @companyid
          AND RTRIM(H.PEXPCODE3) = RTRIM(@ACCODE)
          AND H.DOCDATE <= @TDATE
          AND ISNULL(H.PEXPAMT3, 0) > 0
          and ISNULL(h.IsDeleted, 0) = 0;


        /* =========================================================
           COMPANY 01 ONLY
           WITHHOLDING INCOME TAX
           ========================================================= */

        IF @companyid = 1006
        BEGIN

            INSERT INTO ACCUMULATED
            (
                COCODE,VONO,VODATE,VOTYPE,
                AC1,AC3,CRAMT,ACTYPE,NARRATION
            )
            SELECT
                H.companyid,
                H.INVNO,
                H.WH_IT_REC,
                'WT',
                SUBSTRING(H.CUSCODE,1,3),
                SUBSTRING(H.CUSCODE,4,3),
                ISNULL(H.WH_IT_AMT,0),
                '3',
                'WITH-HOLDING INCOME TAX ON SALES INVOICE # ' +
                ISNULL(CONVERT(VARCHAR(50), H.INVNO),'') +
                ' @ ' +
                ISNULL(CONVERT(VARCHAR(50),H.WH_IT),'') +
                ' %'
            FROM ISSHEAD H
            WHERE H.companyid = @companyid
              AND ISNULL(H.WH_IT_REC,'3000-12-31') <= @TDATE
              AND ISNULL(H.WH_IT_AMT,0) > 0
              AND H.WH_IT_REC IS NOT NULL
              AND RTRIM(H.CUSCODE) = RTRIM(@ACCODE)
          and ISNULL(h.IsDeleted, 0) = 0;


            INSERT INTO ACCUMULATED
            (
                COCODE,VONO,VODATE,VOTYPE,
                AC1,AC3,DRAMT,ACTYPE,NARRATION
            )
            SELECT
                H.companyid,
                H.INVNO,
                H.WH_IT_REC,
                'WT',
                '051',
                '007',
                ISNULL(H.WH_IT_AMT,0),
                '3',
                'W/H I.TAX ON INV.# ' +
                ISNULL(CONVERT(VARCHAR(50),H.INVNO),'') +
                ' @ ' +
                ISNULL(CONVERT(VARCHAR(50),H.WH_IT),'') +
                ' % Cus. ' +
                LEFT(RTRIM(H.CUSNAME),30)
            FROM ISSHEAD H
            WHERE H.companyid = @companyid
              AND ISNULL(H.WH_IT_REC,'3000-12-31') <= @TDATE
              AND ISNULL(H.WH_IT_AMT,0) > 0
              AND H.WH_IT_REC IS NOT NULL
              AND @ACCODE = '051007'
          and ISNULL(h.IsDeleted, 0) = 0;


            /* =====================================================
               WITHHOLDING SALES TAX
               ===================================================== */

            INSERT INTO ACCUMULATED
            (
                COCODE,VONO,VODATE,VOTYPE,
                AC1,AC3,CRAMT,ACTYPE,NARRATION
            )
            SELECT
                H.companyid,
                H.INVNO,
                H.WH_ST_REC,
                'WT',
                SUBSTRING(H.CUSCODE,1,3),
                SUBSTRING(H.CUSCODE,4,3),
                ISNULL(H.WH_ST_AMT,0),
                '3',
                'WITH-HOLDING SALES TAX ON SALES INVOICE # ' +
                ISNULL(CONVERT(VARCHAR(50),H.INVNO),'') +
                ' @ ' +
                ISNULL(CONVERT(VARCHAR(50),H.WH_ST),'') +
                ' %'
            FROM ISSHEAD H
            WHERE H.companyid = @companyid
              AND ISNULL(H.WH_ST_REC,'3000-12-31') <= @TDATE
              AND ISNULL(H.WH_ST_AMT,0) > 0
              AND H.WH_ST_REC IS NOT NULL
              AND RTRIM(H.CUSCODE) = RTRIM(@ACCODE)
          and ISNULL(h.IsDeleted, 0) = 0;


            INSERT INTO ACCUMULATED
            (
                COCODE,VONO,VODATE,VOTYPE,
                AC1,AC3,DRAMT,ACTYPE,NARRATION
            )
            SELECT
                H.companyid,
                H.INVNO,
                H.WH_ST_REC,
                'WT',
                '053',
                '001',
                ISNULL(H.WH_ST_AMT,0),
                '3',
                'WITH-HOLDING SALES TAX ON SALES INVOICE # ' +
                ISNULL(CONVERT(VARCHAR(50),H.INVNO),'') +
                ' @ ' +
                ISNULL(CONVERT(VARCHAR(50),H.WH_ST),'') +
                ' %'
            FROM ISSHEAD H
            WHERE H.companyid = @companyid
              AND ISNULL(H.WH_ST_REC,'3000-12-31') <= @TDATE
              AND ISNULL(H.WH_ST_AMT,0) > 0
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
                    ISNULL(A.DRAMT,0) -
                    ISNULL(A.CRAMT,0)
                )
            FROM ACCUMULATED A
            WHERE A.VODATE < @SDATE
        )
        FROM ACCOPEN AO;


        /* =========================================================
           COMMIT
           ========================================================= */

        RETURN 0;

    END TRY
BEGIN CATCH
    THROW;
END CATCH;

END;

