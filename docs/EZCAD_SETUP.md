# EZCAD2 Text File Setup

Use EZCAD2 v2.14.10 only as the marking engine.

1. Create or open the part template in EZCAD.
2. Add the QR/data-matrix object.
3. Configure the QR object to read data from a text file.
4. Point it to:

   ```text
   C:\Laser\QRDATA.TXT
   ```

5. Save the `.ezd` template.
6. In the app, set the part template path to that `.ezd` file and press `Set Active`.

The app writes one QR payload to `QRDATA.TXT` per serial number. Operators should not edit QR values inside EZCAD.
