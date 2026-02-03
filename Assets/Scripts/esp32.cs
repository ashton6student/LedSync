using System;
using UnityEngine;
using System.IO.Ports;

public class esp32 : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public string portName = "COM4";
    public int baudRate = 115200;
    private SerialPort serialPort;

    public int max = 100;
    private int counter = 0;

    private int flag = 0;
    
    void Start()
    {
        serialPort = new SerialPort(portName, baudRate);
        serialPort.Open();
    }

    // Update is called once per frame
    void Update()
    {
   
        if (counter > max)
        {
           counter = 0;
           flag = ~flag;
        } else
        {
            counter += 1;
        }
        Debug.Log(flag);
        serialPort.Write(flag.ToString());
    }
}
