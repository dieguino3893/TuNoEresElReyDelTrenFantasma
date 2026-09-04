"""
Modelos Pydantic — Subscriber
"""

from pydantic import BaseModel, EmailStr, Field
from datetime import datetime, timezone
from typing import Optional


class SubscriberCreate(BaseModel):
    email: EmailStr
    source: Optional[str] = "web"          # de dónde se suscribió


class SubscriberResponse(BaseModel):
    id: str
    email: str
    source: str
    created_at: datetime
    active: bool


class SubscriberInDB(BaseModel):
    email: str
    source: str = "web"
    created_at: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    active: bool = True
    ip: Optional[str] = None               # para auditoría básica