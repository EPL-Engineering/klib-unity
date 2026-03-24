using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml; 
using System.Xml.Serialization;

using UnityEngine;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ProtoBuf;

namespace KLibU
{ 
    public static class Files
    {
        public static string TimeStamp { get { return DateTime.Now.ToString("yyyyMMdd_HHmmss"); } }

        public static Stream _stream = null;

        public static List<string> EnumerateFolderNames(string rootFolder)
        {
            var folders = new List<string>(Directory.GetDirectories(rootFolder))
                .Select(x => Path.GetFileName(x))
                .ToList();
            folders.Sort();
            return folders;
        }

        public static void JSONSerialize<T>(T t, string path)
        {
            File.WriteAllText(path, JSONSerializeToString(t));
        }

        public static T JSONDeserialize<T>(string path)
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }

        public static string JSONSerializeToString<T>(T t, Newtonsoft.Json.Formatting formatting)
        {
            return JsonConvert.SerializeObject(t, formatting, new StringEnumConverter { CamelCaseText = false });
        }

        public static string JSONSerializeToString<T>(T t)
        {
            return JsonConvert.SerializeObject(t, Newtonsoft.Json.Formatting.Indented, new StringEnumConverter { CamelCaseText = false });
        }

        public static T JSONDeserializeFromString<T>(string text)
        {
            return JsonConvert.DeserializeObject<T>(text);
        }

        public static string JSONStringAdd(string json, string name, string val)
        {
            if (json.Length == 0)
            {
                json = "{" + System.Environment.NewLine;
            }
            else
            {
                json = json.Substring(0, json.Length - 1) + ",";
            }

            json += "\"" + name + "\":" + val + System.Environment.NewLine + "}";
            return json;
        }

        public static void AppendTextFile(string path, string text)
        {
            using (Stream s = File.Open(path, FileMode.Append))
            {
                byte[] b = System.Text.Encoding.ASCII.GetBytes(text);
                s.Write(b, 0, b.Length);
            }
        }

        public static void XmlSerialize<T>(T t, string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (Stream s = File.Create(path)) {
                serializer.Serialize(s, t); 
            }
        }
        
        public static T XmlDeserialize<T>(string path)
		{
            T t;
			XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (Stream s = File.OpenRead(path))
            {
                t = (T)serializer.Deserialize(s);
            }
			
			return t;
		}

        public static T XmlDeserializeFromTextAsset<T>(string assetPath)
        {
            TextAsset textAsset = (TextAsset) Resources.Load(assetPath);
            return XmlDeserializeFromString<T>(textAsset.text);
        }

        public static string XmlSerializeToString<T>(T t)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            XmlSerializer serializer = new XmlSerializer(typeof(T));

            using (var writer = new StringWriter(sb))
            {
                serializer.Serialize(writer, t);
            }

            return sb.ToString();
        }

        public static T XmlDeserializeFromString<T>(string data)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            T t = default(T);

            using (StringReader reader = new StringReader(data))
            {
                t = (T)serializer.Deserialize(reader);
            }
            return t;
        }

        public static void SerializeToBinary<T>(T t, string path)
		{
			using (Stream s = File.Create(path)) {
                Serializer.Serialize<T>(s, t);
			}
		}
		
		public static T DeserializeFromBinary<T>(string path)
		{
			T t;
			using (Stream s = File.OpenRead(path))
            {
                t = Serializer.Deserialize<T>(s);
			}

			return t;
		}
		
        public static void CreateBinarySerialization(string path)
        {
            _stream = File.Create(path);
        }

        public static void OpenBinarySerialization(string path)
        {
            _stream = File.OpenRead(path);
        }

        public static void SerializeToBinary<T>(T t)
        {
            Serializer.SerializeWithLengthPrefix(_stream, t, PrefixStyle.Base128);
            //Serializer.Serialize<T>(_stream, t);
        }

        public static T DeserializeFromBinary<T>()
        {
            return Serializer.DeserializeWithLengthPrefix<T>(_stream, PrefixStyle.Base128);
        }

        public static void CloseBinarySerialization()
        {
            if (_stream != null)
            {
                //_stream.Close();
                _stream.Dispose();
                _stream = null;
            }
        }

        public static void CopyUnique(string from, string to)
        {
            string folder = Path.GetDirectoryName(to);
            string filestem = Path.GetFileNameWithoutExtension(to);
            string ext = Path.GetExtension(to);

            string uniqueTo = to;
            int k = 2;
            while (File.Exists(uniqueTo))
            {
                uniqueTo = Path.Combine(folder, filestem + "_" + k.ToString() + ext);
                k++;
            }

            File.Copy(from, uniqueTo);
        }

        public static List<string> ParseWebFiles(string xmlString)
        {
            return ParseWebFolderData(xmlString, false);
        }

        public static List<string> ParseWebFolders(string xmlString)
        {
            return ParseWebFolderData(xmlString, true);
        }

        private static List<string> ParseWebFolderData(string xmlString, bool folders)
        {
            List<string> remoteFiles = new List<string>();

            if (!string.IsNullOrEmpty(xmlString))
            {
                using (StringReader reader = new StringReader(xmlString))
                using (XmlTextReader xml = new XmlTextReader(reader) { Namespaces = false })
                {
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.Element)
                        {
                            if (xml.Name == "D:href")
                            {
                                xml.Read();
                                string url = xml.Value;
                                if (url[url.Length - 1] != '/' && !folders) remoteFiles.Add(url);
                                if (url[url.Length - 1] == '/' && folders) remoteFiles.Add(url);
                            }
                        }
                    }
                }
            }
            return remoteFiles;
        }

        public static DateTime GetDateFromPropfindResponse(string response, string property)
        {
            DateTime dt = new DateTime();

            using (StringReader reader = new StringReader(response))
            using (XmlTextReader xml = new XmlTextReader(reader) { Namespaces = false })
            {
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.Element)
                    {
                        if (xml.Name == "D:" + property)
                        {
                            xml.Read();
                            dt = DateTime.Parse(xml.Value);
                        }
                    }
                }

            }

            return dt;
        }

        public static List<string> ParseWeb(string xmlString)
        {
            List<string> remoteFiles = new List<string>();

            if (!string.IsNullOrEmpty(xmlString))
            {
                using (StringReader reader = new StringReader(xmlString))
                using (XmlTextReader xml = new XmlTextReader(reader) { Namespaces = false })
                {
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.Element)
                        {
                            if (xml.Name == "D:href")
                            {
                                xml.Read();
                                string url = xml.Value;
                                if (url[url.Length - 1] != '/') remoteFiles.Add(url);
                            }
                        }
                    }
                }
            }
            return remoteFiles;
        }

        public static string ReadString(Stream s, int len)
        {
            byte[] buffer = new byte[len];
            int nread = s.Read(buffer, 0, len);
            return new string(System.Text.Encoding.UTF8.GetChars(buffer));
        }
        public static uint ReadUInt(Stream s)
        {
            byte[] buffer = new byte[4];
            int nread = s.Read(buffer, 0, 4);
            return BitConverter.ToUInt32(buffer, 0);
        }
        public static ushort ReadUShort(Stream s)
        {
            byte[] buffer = new byte[2];
            int nread = s.Read(buffer, 0, 2);
            return BitConverter.ToUInt16(buffer, 0);
        }
        public static double ReadDouble(Stream s)
        {
            byte[] buffer = new byte[8];
            int nread = s.Read(buffer, 0, 8);
            return BitConverter.ToDouble(buffer, 0);
        }

        public static string ExtractJsonObjectString(string json, string name, string next)
        {
            string objIndicator = ",\"" + name + "\":";
            int objStart = json.IndexOf(objIndicator);
            if (objStart < 0)
                throw new Exception("Json object not found: " + name);

            objStart += objIndicator.Length;

            int objEnd = json.Length - 1;
            if (!string.IsNullOrEmpty(next))
            {
            }

            return json.Substring(objStart, objEnd - objStart);
        }

        public static T ExtractJsonObject<T>(string json, string name, string next)
        {
            string objString = ExtractJsonObjectString(json, name, next);
            return JsonConvert.DeserializeObject<T>(objString);
        }

        public static byte[] ToProtoBuf<T>(T obj)
        {
            byte[] pbuf;
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize<T>(ms, obj);
                pbuf = ms.ToArray();
            }
            return pbuf;
        }

        public static T FromProtoBuf<T>(byte[] pbuf)
        {
            T obj = default(T);
            using (var ms = new MemoryStream(pbuf))
            {
                obj = Serializer.Deserialize<T>(ms);
            }

            return obj;
        }

#if UNITY_EDITOR
        public static void DumpF32(string fn, float[] data)
        {
            using (Stream s = File.Create(fn))
            using (BinaryWriter bw = new BinaryWriter(s))
            {
                byte[] b = new byte[data.Length * 4];
                Buffer.BlockCopy(data, 0, b, 0, b.Length);
                bw.Write(b);
            }
        }
#endif

    }
}