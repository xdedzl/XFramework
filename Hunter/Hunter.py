import socket

def start_hunter(ip, port):
	s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
	s.bind((ip, port))

	while True:
		recv_data = s.recvfrom(1024)
		message = decode_message(recv_data[0])
		print(message)

def decode_message(message):
	import struct
	length = len(message)
	m, = struct.unpack_from('{}s'.format(length), message, 0)
	m = m.decode('utf-8','ignore')
	return m

def main(args):
	start_hunter(args.ip, args.port)


if __name__ == '__main__':
	import argparse
	parser = argparse.ArgumentParser(description='hunter')
	parser.add_argument('-ip', type=str, help='', default='')
	parser.add_argument('-port', type=int, help='', default=10001)
	args = parser.parse_args()

	main(args)