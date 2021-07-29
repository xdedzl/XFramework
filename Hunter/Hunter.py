# --*-- coding: utf-8 --*--

import socket
import struct
import _thread
import colorama
from colorama import init,Fore,Back,Style
init(autoreset=True)

class Color(object):
	red = '31'
	green = '32'
	yellow = '33'
	blue = '34'

def get_color_text(text, color):
	return '\033[{}m{}\033[0m'.format(color, text)

def start_hunter(ip, port, game_port):
	s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
	s.bind((ip, port))
	_thread.start_new_thread(receive_input, (s, game_port,))
	receive_message(s)

def receive_message(s):
	while True:
		recv_data = s.recvfrom(1024)
		message = decode_message(recv_data[0])
		print(message)

def receive_input(s, game_port):
	while True:
		strs = input()
		command = encode_message(strs)
		s.sendto(command, ('127.0.0.1', game_port))

def decode_message(message):
	length = len(message)
	m, = struct.unpack_from('{}s'.format(length), message, 0)
	m = m.decode('utf-8','ignore')
	return m

def encode_message(message):
	message = message.encode("utf-8")
	return message

def main(args):
	start_hunter(args.ip, args.port, args.game_port)


if __name__ == '__main__':
	import argparse
	parser = argparse.ArgumentParser(description='hunter')
	parser.add_argument('-ip', type=str, help='', default='')
	parser.add_argument('-port', type=int, help='', default=10001)
	parser.add_argument('-game_port', type=int, help='', default=10002)
	args = parser.parse_args()

	main(args)