# 	SharpAbeebus

**SharpAbeebus is a modern C# .NET 8 rewrite of the original Python 3 [SharpAbeebus](https://github.com/13Cubed/SharpAbeebus) GeoIP lookup utility. It utilizes [ipinfo.io](https://ipinfo.io) services. This program is very useful for parsing email headers, log files, and any other arbitrary data for IPv4 addresses, and then obtaining GeoIP data for each of those addresses.**

For any given file(s), SharpAbeebus will:

- Extract valid IPv4 addresses (e.g., "CSI: Cyber" addresses like `951.27.9.840` will not match)
- Ignore duplicates
- Ignore bogon addresses, the loopback network, link local addresses, and RFC 1918 (private) addresses

For each remaining address, SharpAbeebus will provide the following data as available from ipinfo.io:

**- IP Address, Hostname, Country, Region, City, Postal Code, Latitude, Longitude, ASN, Count**

By default, SharpAbeebus will display the data to stdout in the following format:

```
IP Address    | Hostname                                  | Country | Region   | City    | Postal Code | Latitude | Longitude | ASN                     | Count
52.73.116.225 | ec2-52-73-116-225.compute-1.amazonaws.com | US      | Virginia | Ashburn | 20149       | 39.0437  | -77.4875  | AS14618 Amazon.com Inc. | 5
```

- Using the `-w` option, you can provide a filename to which SharpAbeebus will output the data in CSV format (useful for working with large data sets in **Timeline Explorer**, **Microsoft Excel**, or **LibreOffice Calc**):

```
IP Address,Hostname,Country,Region,City,Postal Code,Latitude,Longitude,ASN,Count
52.73.116.225,ec2-52-73-116-225.compute-1.amazonaws.com,US,Virginia,Ashburn,20149,39.0437,-77.4875,AS14618 Amazon.com Inc.,5
```

- Using the `-a` option, you can provide an **ipinfo.io API** key if you have large datasets to process.

## Requirements

SharpAbeebus requires [.NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
