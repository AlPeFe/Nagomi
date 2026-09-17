"""Hash compatible con ASP.NET Core Identity v3, con la cabecera completa de 13 bytes.

Formato real (lo que espera VerifyHashedPasswordV3):
  [0]    0x01
  [1..4] PRF (1 = HMAC-SHA256), big-endian
  [5..8] iteraciones, big-endian
  [9..12] tamaño del salt (16), big-endian
  [13..28] salt
  [29..60] subclave (32 bytes)
"""
import base64
import hashlib
import os
import struct
import sys

def identity_v3_hash(password: str, iterations: int = 100_000) -> str:
    salt = os.urandom(16)
    subkey = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, iterations, dklen=32)
    header = b"\x01" + struct.pack(">III", 1, iterations, len(salt))
    return base64.b64encode(header + salt + subkey).decode("ascii")

if __name__ == "__main__":
    print(identity_v3_hash(sys.argv[1]))
