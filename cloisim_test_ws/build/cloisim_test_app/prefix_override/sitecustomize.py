import sys
if sys.prefix == '/usr':
    sys.real_prefix = sys.prefix
    sys.prefix = sys.exec_prefix = '/home/nav/workspace/cloisim/cloisim/cloisim_test_ws/install/cloisim_test_app'
