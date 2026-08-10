# Assinatura Azure for Students. Fixa aqui por conveniência; pode ser
# sobrescrita por TF_VAR_subscription_id.
variable "subscription_id" {
  type    = string
  default = "3082c55f-05e0-4db1-945d-6587222f8394"
}

variable "location" {
  type    = string
  default = "brazilsouth"
}

variable "prefix" {
  type    = string
  default = "jdice"
}

variable "ghcr_owner" {
  type    = string
  default = "tamotsutozaki"
}

# Tag das imagens no GHCR. "latest" acompanha a main; pode-se fixar um SHA.
variable "image_tag" {
  type    = string
  default = "latest"
}

# ── Segredos: vêm do script up (TF_VAR_*), nunca com valor padrão no código ──

variable "postgres_password" {
  type      = string
  sensitive = true
}

variable "jwt_signing_key" {
  type      = string
  sensitive = true
}

variable "rabbitmq_password" {
  type      = string
  sensitive = true
}

variable "seed_admin_email" {
  type    = string
  default = "admin@jdice.local"
}

variable "seed_admin_password" {
  type      = string
  sensitive = true
}
