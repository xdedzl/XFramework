	# --*-- coding: utf-8 --*--

import socket
import struct
from Logger import Logger
import _thread
import colorama
from colorama import init,Fore,Back,Style
init(autoreset=True)

class Color(object):
	RED = '31'
	GREEN = '32'
	YELLOW = '33'
	BLUE = '34'

class MessageType(object):
	NORMAL = 0
	WARNING = 1
	ERROR = 2
	SYSTEM = 3
	INPUT = 4
	OUTPUT = 5
	UNITY = 6

class MessageSource(object):
	XCONSOLE = 0
	UNITY = 1

TYPE_2_COLOR = {
	MessageType.WARNING: Color.YELLOW,
	MessageType.ERROR: Color.RED,
	MessageType.SYSTEM: Color.GREEN,
	MessageType.INPUT: Color.GREEN,
}
		

port = None
game_port = None
game_ip = ''

logger = Logger('client_log')


def get_color_text(text, color):
	return '\033[{}m{}\033[0m'.format(color, text)

def start_hunter():
	"""
	开启hunter
	"""
	s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
	s.bind(('', port))
	_thread.start_new_thread(receive_input, (s, game_port,))
	

	print('dsadada')
	receive_message(s)

def receive_message(s):
	"""
	监听客户端log
	"""
	while True:
		recv_data = s.recvfrom(2048)
		m_type, m_source, m = decode_message(recv_data[0])
		if m_type < 0 and m_source < 0:
			global game_ip
			game_ip = m
			print('客户端已连接  ' + game_ip)
		else:
			log_message(m_type, m_source, m)

def receive_input(s, game_port):
	"""
	接收控制台输入，向客户端发送命令
	"""
	# a = get_color_text('>>>', Color.GREEN)
	while True:
		strs = input('>>>')
		if game_ip:
			command = encode_message(strs)
			s.sendto(command, (game_ip, game_port))
		else:
			print('客户端hunter未开启')

def decode_message(message):
	"""
	消息解码
	"""
	message_type, message_source, length = struct.unpack_from('3i', message, 0)
	message, = struct.unpack_from('{}s'.format(length), message, 12)
	message = message.decode('utf-8','ignore')
	return message_type, message_source, message

def encode_message(message):
	"""
	命令编码
	"""
	message = message.encode("utf-8")
	return message

def log_message(m_type, m_source, m):
	"""
	控制台打印log并记录到log文件中
	"""
	logger.log(m)
	color = TYPE_2_COLOR.get(m_type, None)
	if color:
		m = get_color_text(m, color)
	print(m)

def main(args):
	global port
	global game_port
	port = args.port
	game_port = args.game_port
	start_hunter()


if __name__ == '__main__':
	import argparse
	parser = argparse.ArgumentParser(description='hunter')
	parser.add_argument('-port', type=int, help='', default=10001)
	parser.add_argument('-game_port', type=int, help='', default=10002)
	args = parser.parse_args()
	main(args)
