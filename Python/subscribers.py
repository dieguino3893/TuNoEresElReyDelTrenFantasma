"""
Router — Suscriptores
POST /subscribe    → registrar email
GET  /subscribers  → listar (protegido con API key interna)
DELETE /unsubscribe → baja de suscripción
"""

from fastapi import APIRouter, HTTPException, status, Request, Header
from pymongo.errors import DuplicateKeyError
from bson import ObjectId
from datetime import datetime, timezone
import os

from database import get_db
from models import SubscriberCreate, SubscriberInDB, SubscriberResponse

router = APIRouter()

ADMIN_API_KEY = os.getenv("ADMIN_API_KEY", "change-me-in-production")


# ── POST /subscribe ───────────────────────────────────────────────────────────
@router.post(
    "/subscribe",
    status_code=status.HTTP_201_CREATED,
    summary="Registrar un nuevo suscriptor",
)
async def subscribe(payload: SubscriberCreate, request: Request):
    db = get_db()

    subscriber = SubscriberInDB(
        email=payload.email,
        source=payload.source,
        ip=request.client.host if request.client else None,
    )

    try:
        result = await db.subscribers.insert_one(subscriber.model_dump())
    except DuplicateKeyError:
        # Email ya existe → igual devolvemos 200 para no revelar si el email está en BD
        return {"message": "¡Gracias! Te avisaremos con las novedades."}

    return {
        "message": "¡Suscripción exitosa! Te avisaremos con las novedades.",
        "id": str(result.inserted_id),
    }


# ── GET /subscribers (admin) ──────────────────────────────────────────────────
@router.get(
    "/subscribers",
    summary="Listar suscriptores (requiere API key)",
)
async def list_subscribers(
    x_api_key: str = Header(..., description="API key de administrador"),
    skip: int = 0,
    limit: int = 100,
):
    if x_api_key != ADMIN_API_KEY:
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="No autorizado")

    db = get_db()
    cursor = db.subscribers.find({"active": True}).skip(skip).limit(limit)
    subscribers = []

    async for doc in cursor:
        subscribers.append({
            "id": str(doc["_id"]),
            "email": doc["email"],
            "source": doc.get("source", "web"),
            "created_at": doc["created_at"].isoformat(),
            "active": doc["active"],
        })

    total = await db.subscribers.count_documents({"active": True})
    return {"total": total, "subscribers": subscribers}


# ── DELETE /unsubscribe ───────────────────────────────────────────────────────
@router.delete(
    "/unsubscribe",
    summary="Dar de baja un suscriptor",
)
async def unsubscribe(email: str):
    db = get_db()
    result = await db.subscribers.update_one(
        {"email": email},
        {"$set": {"active": False, "unsubscribed_at": datetime.now(timezone.utc)}},
    )

    if result.matched_count == 0:
        raise HTTPException(status_code=404, detail="Email no encontrado")

    return {"message": "Te has dado de baja correctamente."}