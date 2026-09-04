"""
Conexión a MongoDB / Azure Cosmos DB
"""

import os
from motor.motor_asyncio import AsyncIOMotorClient

_client: AsyncIOMotorClient | None = None
_db = None


async def connect_db():
    global _client, _db
    uri = os.getenv("MONGODB_URI", "mongodb://localhost:27017")
    db_name = os.getenv("MONGODB_DB", "banda_web")

    _client = AsyncIOMotorClient(uri)
    _db = _client[db_name]

    # Crear índice único en email para evitar duplicados
    await _db.subscribers.create_index("email", unique=True)
    print(f"✅  Conectado a MongoDB · base de datos: {db_name}")


async def close_db():
    global _client
    if _client:
        _client.close()
        print("🔌  Conexión MongoDB cerrada")


def get_db():
    if _db is None:
        raise RuntimeError("Base de datos no inicializada")
    return _db