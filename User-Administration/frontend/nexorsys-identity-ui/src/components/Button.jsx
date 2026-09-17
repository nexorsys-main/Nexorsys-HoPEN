import React from "react";

const Button = ({
  children,
  variant = "primary",
  onClick,
  disabled = false,
  className = "",
}) => {
  const baseClasses = "px-4 py-2 rounded transition-colors ";
  const variantClasses =
    variant === "primary"
      ? "bg-nexorsys-blue hover:bg-blue-600 text-slate-900"
      : "bg-nexorsys-gray hover:bg-slate-100 text-slate-900";
  const disabledClasses = disabled ? "opacity-50 cursor-not-allowed" : "";

  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className={`${baseClasses}${variantClasses}${disabledClasses} ${className}`}
    >
      {children}
    </button>
  );
};

export default Button;
