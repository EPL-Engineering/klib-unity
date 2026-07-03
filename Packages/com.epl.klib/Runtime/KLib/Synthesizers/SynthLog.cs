using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

using KLibU.Logging;

namespace KLibU.Synthesizers
{
    [JsonObject(MemberSerialization.OptOut)]
    public class SynthLog
    {
        private string _name;
        private List<string> _variableNames;
        private float[] _data;
        private long[] _timeStamps;

        [JsonIgnore]
        private DateTime _startTime;
        [JsonIgnore]
        private int _dataIndex;
        [JsonIgnore]
        private int _timeIndex;
        [JsonIgnore]
        private int _lengthIncrement;

        public SynthLog(string name, params string[] variableNames) : this(name, variableNames.ToList()) { }

        public SynthLog(string name, List<string> variableNames)
        {
            _startTime = DateTime.Now;
            _name = name;
            _variableNames = variableNames;

            _lengthIncrement = 60 * 300; // 5 minutes at 60fps
            _data = new float[_lengthIncrement * _variableNames.Count];
            _timeStamps = new long[_lengthIncrement];

            _dataIndex = 0;
            _timeIndex = 0;
        }

        public void SetName(string name)
        {
            _name = name;
        }   

        public void Add(params float[] newData)
        {
            if (_dataIndex + newData.Length >= _data.Length)
            {
                Array.Resize(ref _data, _data.Length + _lengthIncrement * _variableNames.Count);
                Array.Resize(ref _timeStamps, _data.Length + _lengthIncrement);
            }

            _timeStamps[_timeIndex++] = HighPrecisionClock.UtcNowIn100nsTicks;
            for (int k = 0; k < newData.Length; k++)
            {
                _data[_dataIndex++] = newData[k];
            }
        }

        public void SaveBinary(string folder)
        {
            string filename = $"SynthLog-{_name}-{_startTime.ToString("yyyyMMdd-HHmmss")}.bin";
            string fullpath = Path.Combine(folder, filename);
            using (FileStream fs = new FileStream(fullpath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write(this.ToByteArray());
                bw.Close();
                fs.Close();
            }
        }

        public void SaveJson(string folder)
        {
            string filename = $"SynthLog-{_name}-{_startTime.ToString("yyyyMMdd-HHmmss")}.json";
            string fullpath = Path.Combine(folder, filename);
            KLibU.Files.JSONSerialize(this, fullpath);
        }

        public SynthLog Trim()
        {
            Array.Resize(ref _data, _dataIndex);
            Array.Resize(ref _timeStamps, _timeIndex);
            return this;
        }

        private int CalculateTotalBytes()
        {
            int nbytes =
                7 * sizeof(Int32) +         /* total + 6 lengths below */
                System.Text.Encoding.UTF8.GetByteCount("SynthLog") + /* indicator */
                System.Text.Encoding.UTF8.GetByteCount(_name) + /* name */
                _variableNames.Sum(s => System.Text.Encoding.UTF8.GetByteCount(s) + 1) + /* variable names (with null terminators) */
                _timeIndex * sizeof(long) + /* high-precision timestamps */
                _dataIndex * sizeof(float); /* main data */
            return nbytes;
        }

        public byte[] ToByteArray()
        {
            // Indicator
            byte[] dataType = System.Text.Encoding.UTF8.GetBytes("SynthLog");

            // Concatenate variables names list into single delimited string (as byte array)
            byte[] varNames = _variableNames.SelectMany(s => System.Text.Encoding.UTF8.GetBytes(s + "\0")).ToArray();

            int nbytes = CalculateTotalBytes();

            // Initialize byte array
            byte[] bytes = new byte[nbytes];
            int offset = 0;

            // Total length of data (bytes array)
            Buffer.BlockCopy(BitConverter.GetBytes(nbytes), 0, bytes, offset, 4);
            offset += 4;

            // Indicator...
            // ...length
            Buffer.BlockCopy(BitConverter.GetBytes(dataType.Length), 0, bytes, offset, 4);
            offset += 4;
            // ...string
            Buffer.BlockCopy(dataType, 0, bytes, offset, dataType.Length);
            offset += dataType.Length;

            // Name...
            // ...length
            Buffer.BlockCopy(BitConverter.GetBytes(_name.Length), 0, bytes, offset, 4);
            offset += 4;
            // ...string
            Buffer.BlockCopy(System.Text.Encoding.UTF8.GetBytes(_name), 0, bytes, offset, _name.Length);
            offset += _name.Length;

            // Variable names...
            // ...length
            Buffer.BlockCopy(BitConverter.GetBytes(varNames.Length), 0, bytes, offset, 4);
            offset += 4;
            // ...byte array
            Buffer.BlockCopy(varNames, 0, bytes, offset, varNames.Length);
            offset += varNames.Length;

            // High-precision time stamps...
            // ... length
            Buffer.BlockCopy(BitConverter.GetBytes(_timeIndex), 0, bytes, offset, 4);
            offset += 4;
            // ... byte array
            Buffer.BlockCopy(_timeStamps, 0, bytes, offset, _timeIndex * sizeof(long));
            offset += _timeIndex * sizeof(long);

            // Main data...
            // ... length
            Buffer.BlockCopy(BitConverter.GetBytes(_dataIndex), 0, bytes, offset, 4);
            offset += 4;
            // ... byte array
            Buffer.BlockCopy(_data, 0, bytes, offset, _dataIndex * sizeof(float));
            offset += _dataIndex * sizeof(float);   

            return bytes;
        }
    }

}