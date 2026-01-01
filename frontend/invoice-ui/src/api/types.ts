export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  // Allow extensions like traceId, errorCode, etc.
  [key: string]: unknown;
};

export class ApiError extends Error {
  public readonly status: number;
  public readonly problemDetails?: ProblemDetails;
  public readonly rawBody?: string;

  constructor(args: { message: string; status: number; problemDetails?: ProblemDetails; rawBody?: string }) {
    super(args.message);
    this.name = "ApiError";
    this.status = args.status;
    this.problemDetails = args.problemDetails;
    this.rawBody = args.rawBody;
  }
}