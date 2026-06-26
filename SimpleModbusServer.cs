using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

class SimpleModbusServer
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool[] _coils = new bool[10];
    private bool[] _discreteInputs = new bool[10];
    private ushort[] _holdingRegisters = new ushort[10];
    private ushort[] _inputRegisters = new ushort[10];

    public async Task StartAsync(int port = 502)
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Console.WriteLine($"Modbus TCP server started on port {port}");

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = HandleClientAsync(client, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server stopped");
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
        var stream = client.GetStream();
        var buffer = new byte[1024];

        try
        {
            while (!token.IsCancellationRequested && client.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                if (bytesRead == 0) break;

                var response = ProcessModbusRequest(buffer, bytesRead);
                if (response != null)
                {
                    await stream.WriteAsync(response, 0, response.Length, token);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error: {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine($"Client disconnected");
        }
    }

    private byte[]? ProcessModbusRequest(byte[] request, int length)
    {
        if (length < 8) return null;

        // Modbus TCP MBAP header (6 bytes) + PDU
        byte transactionIdHigh = request[0];
        byte transactionIdLow = request[1];
        byte protocolIdHigh = request[2];
        byte protocolIdLow = request[3];
        byte lengthHigh = request[4];
        byte lengthLow = request[5];
        byte unitId = request[6];
        byte functionCode = request[7];

        Console.WriteLine($"Function: {functionCode}");

        try
        {
            byte[]? pdu = null;

            switch (functionCode)
            {
                case 1: // Read Coils
                    pdu = HandleReadCoils(request, length);
                    break;
                case 2: // Read Discrete Inputs
                    pdu = HandleReadDiscreteInputs(request, length);
                    break;
                case 3: // Read Holding Registers
                    pdu = HandleReadHoldingRegisters(request, length);
                    break;
                case 4: // Read Input Registers
                    pdu = HandleReadInputRegisters(request, length);
                    break;
                case 5: // Write Single Coil
                    pdu = HandleWriteSingleCoil(request, length);
                    break;
                case 6: // Write Single Register
                    pdu = HandleWriteSingleRegister(request, length);
                    break;
                default:
                    pdu = new byte[] { (byte)(functionCode | 0x80), 0x01 }; // Exception code 1
                    break;
            }

            if (pdu == null) return null;

            // Build response
            int responseLength = 6 + pdu.Length;
            var response = new byte[responseLength];
            response[0] = transactionIdHigh;
            response[1] = transactionIdLow;
            response[2] = protocolIdHigh;
            response[3] = protocolIdLow;
            response[4] = (byte)((responseLength - 6) >> 8);
            response[5] = (byte)((responseLength - 6) & 0xFF);
            response[6] = unitId;
            Array.Copy(pdu, 0, response, 7, pdu.Length);

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing request: {ex.Message}");
            return null;
        }
    }

    private byte[] HandleReadCoils(byte[] request, int length)
    {
        ushort startAddress = (ushort)((request[8] << 8) | request[9]);
        ushort quantity = (ushort)((request[10] << 8) | request[11]);

        byte byteCount = (byte)((quantity + 7) / 8);
        var response = new byte[2 + byteCount];
        response[0] = 1; // Function code
        response[1] = byteCount;

        for (int i = 0; i < quantity; i++)
        {
            int byteIndex = i / 8;
            int bitIndex = i % 8;
            if (startAddress + i < _coils.Length && _coils[startAddress + i])
            {
                response[2 + byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        return response;
    }

    private byte[] HandleReadDiscreteInputs(byte[] request, int length)
    {
        ushort startAddress = (ushort)((request[8] << 8) | request[9]);
        ushort quantity = (ushort)((request[10] << 8) | request[11]);

        byte byteCount = (byte)((quantity + 7) / 8);
        var response = new byte[2 + byteCount];
        response[0] = 2; // Function code
        response[1] = byteCount;

        for (int i = 0; i < quantity; i++)
        {
            int byteIndex = i / 8;
            int bitIndex = i % 8;
            if (startAddress + i < _discreteInputs.Length && _discreteInputs[startAddress + i])
            {
                response[2 + byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        return response;
    }

    private byte[] HandleReadHoldingRegisters(byte[] request, int length)
    {
        ushort startAddress = (ushort)((request[8] << 8) | request[9]);
        ushort quantity = (ushort)((request[10] << 8) | request[11]);

        byte byteCount = (byte)(quantity * 2);
        var response = new byte[2 + byteCount];
        response[0] = 3; // Function code
        response[1] = byteCount;

        for (int i = 0; i < quantity; i++)
        {
            if (startAddress + i < _holdingRegisters.Length)
            {
                response[2 + i * 2] = (byte)(_holdingRegisters[startAddress + i] >> 8);
                response[2 + i * 2 + 1] = (byte)(_holdingRegisters[startAddress + i] & 0xFF);
            }
        }

        return response;
    }

    private byte[] HandleReadInputRegisters(byte[] request, int length)
    {
        ushort startAddress = (ushort)((request[8] << 8) | request[9]);
        ushort quantity = (ushort)((request[10] << 8) | request[11]);

        byte byteCount = (byte)(quantity * 2);
        var response = new byte[2 + byteCount];
        response[0] = 4; // Function code
        response[1] = byteCount;

        for (int i = 0; i < quantity; i++)
        {
            if (startAddress + i < _inputRegisters.Length)
            {
                response[2 + i * 2] = (byte)(_inputRegisters[startAddress + i] >> 8);
                response[2 + i * 2 + 1] = (byte)(_inputRegisters[startAddress + i] & 0xFF);
            }
        }

        return response;
    }

    private byte[] HandleWriteSingleCoil(byte[] request, int length)
    {
        ushort outputAddress = (ushort)((request[8] << 8) | request[9]);
        ushort outputValue = (ushort)((request[10] << 8) | request[11]);
        bool value = outputValue == 0xFF00;

        if (outputAddress < _coils.Length)
        {
            _coils[outputAddress] = value;
        }

        var response = new byte[5];
        response[0] = 5; // Function code
        response[1] = (byte)(outputAddress >> 8);
        response[2] = (byte)(outputAddress & 0xFF);
        response[3] = (byte)(outputValue >> 8);
        response[4] = (byte)(outputValue & 0xFF);

        return response;
    }

    private byte[] HandleWriteSingleRegister(byte[] request, int length)
    {
        ushort registerAddress = (ushort)((request[8] << 8) | request[9]);
        ushort registerValue = (ushort)((request[10] << 8) | request[11]);

        if (registerAddress < _holdingRegisters.Length)
        {
            _holdingRegisters[registerAddress] = registerValue;
        }

        var response = new byte[5];
        response[0] = 6; // Function code
        response[1] = (byte)(registerAddress >> 8);
        response[2] = (byte)(registerAddress & 0xFF);
        response[3] = (byte)(registerValue >> 8);
        response[4] = (byte)(registerValue & 0xFF);

        return response;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var server = new SimpleModbusServer();
        Console.WriteLine("Starting Modbus TCP server...");
        Console.WriteLine("Press Ctrl+C to stop");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            server.Stop();
        };

        try
        {
            await server.StartAsync(502);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
        }
    }
}