/**
 * Espelha a PasswordPolicy do domínio no backend. Aqui serve só para avisar
 * antes de enviar — quem decide continua sendo o servidor, que valida de novo.
 */
export const PASSWORD_POLICY = {
  minimumLength: 12,
  maximumLength: 128,
} as const;
